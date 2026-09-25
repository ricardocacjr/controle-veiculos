// Sessão lembrada no aparelho: o motorista abre o app (ícone na tela de início) e já cai no
// "uso", sem digitar PIN toda vez. Só o token JWT (assinado, expira) — nunca o PIN.
window.cvSessao = {
    ler: () => {
        try { return JSON.parse(localStorage.getItem("cv.sessao")); } catch { return null; }
    },
    salvar: (sessao) => {
        try { localStorage.setItem("cv.sessao", JSON.stringify(sessao)); } catch { }
    },
    limpar: () => {
        try { localStorage.removeItem("cv.sessao"); } catch { }
    },
};

window.cvVibrar = (ms) => {
    try { if (navigator.vibrate) navigator.vibrate(ms); } catch { }
};

// ---------------------------------------------------------------------------------------------
// Envio de arquivos DIRETO do celular pra Api (fetch HTTPS normal).
//
// Por quê: no Blazor Server o arquivo escolhido num <InputFile> viaja pela conexão SignalR do
// painel. No iPhone, abrir a câmera põe o Safari em segundo plano e o iOS derruba essa conexão —
// ao voltar, o envio ficava preso pra sempre em "Lendo o painel...". Aqui a foto vai por uma
// requisição comum (o navegador cuida de reenvio/rede), e pro .NET só volta o resultado (JSON
// pequeno), com novas tentativas enquanto a tela reconecta.
// ---------------------------------------------------------------------------------------------
window.cvEnvio = (() => {
    const esperar = (ms) => new Promise(r => setTimeout(r, ms));

    async function enviar(cfg, blob, nomeArquivo, extras) {
        const fd = new FormData();
        fd.append("file", blob, nomeArquivo);
        for (const [k, v] of Object.entries(cfg.campos || {})) if (v !== null && v !== undefined) fd.append(k, v);
        for (const [k, v] of Object.entries(extras || {})) if (v !== null && v !== undefined) fd.append(k, v);

        let ultimo = null;
        for (let tentativa = 0; tentativa < 3; tentativa++) {
            if (tentativa > 0) await esperar(3000);
            const ctrl = new AbortController();
            const limite = setTimeout(() => ctrl.abort(), 90000);
            try {
                const resp = await fetch(cfg.url, {
                    method: cfg.metodo || "POST",
                    headers: cfg.token ? { Authorization: "Bearer " + cfg.token } : {},
                    body: fd,
                    signal: ctrl.signal,
                });
                const corpo = await resp.text();
                ultimo = { ok: resp.ok, status: resp.status, corpo };
                // 502/503/504 = servidor acordando (Render grátis) — tenta de novo.
                if (![502, 503, 504].includes(resp.status)) return ultimo;
            } catch (e) {
                ultimo = {
                    ok: false, status: 0,
                    corpo: e && e.name === "AbortError"
                        ? "O envio demorou demais (internet fraca?). Tente de novo."
                        : "Não consegui enviar (sem internet?). Tente de novo.",
                };
            } finally {
                clearTimeout(limite);
            }
        }
        return ultimo;
    }

    // Avisa o .NET, tentando de novo enquanto o circuito do Blazor está reconectando.
    async function avisar(dotnet, metodo, arg) {
        for (let i = 0; i < 20; i++) {
            try { return await dotnet.invokeMethodAsync(metodo, arg); }
            catch { await esperar(1500); }
        }
    }

    return { enviar, avisar, esperar };
})();

// Foto reduzida no próprio celular (foto do iPhone tem 3-5 MB; isso sobe ~300 KB).
window.cvImagem = {
    reduzir: async (file, lado, qualidade) => {
        try {
            let bitmap;
            try { bitmap = await createImageBitmap(file, { imageOrientation: "from-image" }); }
            catch { bitmap = await createImageBitmap(file); }
            const escala = Math.min(1, lado / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement("canvas");
            canvas.width = Math.round(bitmap.width * escala);
            canvas.height = Math.round(bitmap.height * escala);
            canvas.getContext("2d").drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(r => canvas.toBlob(r, "image/jpeg", qualidade || 0.85));
            return blob || file;
        } catch {
            return file; // não conseguiu reduzir — manda a original mesmo
        }
    },
};

// Botão de câmera: <input type=file> controlado aqui (não pelo InputFile do Blazor).
window.cvCamera = {
    configurar: (input, dotnet, cfg) => {
        if (!input) return;
        input._cvCfg = cfg;
        input._cvDotnet = dotnet;
        if (input._cvLigado) return;
        input._cvLigado = true;

        input.addEventListener("change", async () => {
            const file = input.files && input.files[0];
            const c = input._cvCfg, net = input._cvDotnet;
            input.value = ""; // permite escolher a mesma foto de novo
            if (!file || !c) return;

            cvEnvio.avisar(net, "Inicio");
            const geoPromessa = c.geo ? window.getGeolocation() : Promise.resolve(null);
            const blob = await cvImagem.reduzir(file, c.lado || 1600, 0.85);
            const geo = await geoPromessa;
            const extras = c.enviarGeo && geo && geo.lat != null ? { latitude: geo.lat, longitude: geo.lng } : {};
            const r = await cvEnvio.enviar(c, blob, "foto.jpg", extras);
            await cvEnvio.avisar(net, "Resultado", {
                ok: !!(r && r.ok), status: r ? r.status : 0, corpo: r ? r.corpo : null,
                lat: geo ? geo.lat : null, lng: geo ? geo.lng : null, geoErro: geo ? geo.error : null,
            });
        });
    },
};

// Gravador de voz em WAV 16 kHz mono (LINEAR16). O MediaRecorder do iPhone só grava AAC/MP4,
// que o Google Speech-to-Text v1 não aceita — por isso captura o áudio cru e monta o WAV aqui.
window.cvGravador = (() => {
    let stream, ctx, source, proc, pedacos = [], taxa = 44100;

    function paraWav(amostras, taxaOrigem) {
        const alvo = 16000;
        const passo = taxaOrigem / alvo;
        const n = Math.floor(amostras.length / passo);
        const pcm = new Int16Array(n);
        for (let i = 0; i < n; i++) {
            const v = Math.max(-1, Math.min(1, amostras[Math.floor(i * passo)]));
            pcm[i] = v < 0 ? v * 0x8000 : v * 0x7fff;
        }
        const buffer = new ArrayBuffer(44 + pcm.length * 2);
        const d = new DataView(buffer);
        const txt = (o, s) => { for (let i = 0; i < s.length; i++) d.setUint8(o + i, s.charCodeAt(i)); };
        txt(0, "RIFF"); d.setUint32(4, 36 + pcm.length * 2, true); txt(8, "WAVE");
        txt(12, "fmt "); d.setUint32(16, 16, true); d.setUint16(20, 1, true); d.setUint16(22, 1, true);
        d.setUint32(24, alvo, true); d.setUint32(28, alvo * 2, true); d.setUint16(32, 2, true); d.setUint16(34, 16, true);
        txt(36, "data"); d.setUint32(40, pcm.length * 2, true);
        new Int16Array(buffer, 44).set(pcm);
        return new Blob([buffer], { type: "audio/wav" });
    }

    async function pararCaptura() {
        try {
            source && source.disconnect();
            proc && proc.disconnect();
            stream && stream.getTracks().forEach(t => t.stop());
            ctx && await ctx.close();
        } catch { }
        const total = pedacos.reduce((s, p) => s + p.length, 0);
        if (total === 0) return null;
        const tudo = new Float32Array(total);
        let o = 0;
        for (const p of pedacos) { tudo.set(p, o); o += p.length; }
        pedacos = [];
        return paraWav(tudo, taxa);
    }

    return {
        iniciar: async () => {
            try {
                stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                ctx = new (window.AudioContext || window.webkitAudioContext)();
                taxa = ctx.sampleRate;
                source = ctx.createMediaStreamSource(stream);
                proc = ctx.createScriptProcessor(4096, 1, 1);
                pedacos = [];
                proc.onaudioprocess = e => pedacos.push(new Float32Array(e.inputBuffer.getChannelData(0)));
                source.connect(proc);
                proc.connect(ctx.destination);
                return null;
            } catch (e) {
                return "Não consegui usar o microfone: " + (e && e.message ? e.message : e);
            }
        },
        cancelar: async () => { await pararCaptura(); },
        // Para, monta o WAV e envia direto pra Api (mesmo motivo do cvCamera).
        pararEEnviar: async (cfg) => {
            const wav = await pararCaptura();
            if (!wav) return { ok: false, status: 0, corpo: "Não captei nenhum som. Tente de novo." };
            return await cvEnvio.enviar(cfg, wav, "nota.wav", {});
        },
    };
})();

// Reconexão do painel (Blazor) ao voltar da câmera/outro app: aviso amigável e, se a conexão
// antiga não voltar, recarrega sozinho — a sessão salva no aparelho mantém a pessoa logada.
(() => {
    const observar = () => {
        const modal = document.getElementById("components-reconnect-modal");
        if (!modal) return;
        new MutationObserver(() => {
            const c = modal.classList;
            if (c.contains("components-reconnect-failed") || c.contains("components-reconnect-rejected")) {
                setTimeout(() => location.reload(), 800);
            }
        }).observe(modal, { attributes: true, attributeFilter: ["class"] });
    };
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", observar);
    else observar();

    // Voltou pro app depois de um tempo e a conexão caiu sem ninguém perceber? Recarrega.
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState !== "visible") return;
        setTimeout(() => {
            const modal = document.getElementById("components-reconnect-modal");
            if (modal && (modal.classList.contains("components-reconnect-failed") || modal.classList.contains("components-reconnect-rejected")))
                location.reload();
        }, 1500);
    });
})();
