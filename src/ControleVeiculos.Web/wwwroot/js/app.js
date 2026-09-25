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

// Baixa um arquivo protegido da Api (planilha do relatório) e entrega como download do navegador.
// Devolve null se deu certo, ou a mensagem de erro.
window.cvBaixar = async (url, token, nomeArquivo) => {
    for (let tentativa = 0; tentativa < 3; tentativa++) {
        if (tentativa > 0) await cvEnvio.esperar(3000);
        try {
            const resp = await fetch(url, { headers: { Authorization: "Bearer " + token } });
            if ([502, 503, 504].includes(resp.status)) continue; // servidor acordando
            if (!resp.ok) return (await resp.text()) || `Erro ${resp.status}`;
            const blob = await resp.blob();
            const link = document.createElement("a");
            link.href = URL.createObjectURL(blob);
            link.download = nomeArquivo;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(link.href), 60000);
            return null;
        } catch {
            return "Não consegui baixar (sem internet?). Tente de novo.";
        }
    }
    return "O servidor está acordando. Tente de novo em alguns segundos.";
};

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

            // Foto de perfil: primeiro a pessoa ajusta o recorte (quadrado), depois envia.
            let recortada = null;
            if (c.recorte) {
                recortada = await cvRecorte.abrir(file, c.lado || 400);
                if (!recortada) return; // cancelou
            }

            cvEnvio.avisar(net, "Inicio");
            const geoPromessa = c.geo ? window.getGeolocation() : Promise.resolve(null);
            const blob = recortada || await cvImagem.reduzir(file, c.lado || 1600, 0.85);
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

// Recorte da foto de perfil: moldura quadrada fixa (mesmo formato do perfil na tela de entrada);
// a pessoa arrasta a foto e aproxima (controle deslizante, pinça com dois dedos ou roda do mouse).
// A foto sempre cobre a moldura inteira. Devolve um JPEG quadrado ou null se cancelar.
window.cvRecorte = {
    abrir: (file, lado) => new Promise((resolve) => {
        const url = URL.createObjectURL(file);
        const ov = document.createElement("div");
        ov.className = "crop-overlay";
        ov.innerHTML = `
            <div class="crop-top">
                <div class="crop-title">Ajuste sua foto</div>
                <div class="crop-sub">Arraste para posicionar. Use o controle (ou dois dedos) para aproximar.</div>
            </div>
            <div class="crop-palco"><div class="crop-area"><img class="crop-img" alt="" draggable="false"><div class="crop-frame"></div></div></div>
            <div class="crop-zoom"><span>−</span><input type="range" min="1" max="4" step="0.01" value="1" aria-label="Zoom"><span>+</span></div>
            <div class="crop-actions">
                <button type="button" class="btn btn-soft btn-app crop-cancel">Cancelar</button>
                <button type="button" class="btn btn-primary btn-app crop-ok" disabled>Usar esta foto</button>
            </div>`;
        document.body.appendChild(ov);

        const area = ov.querySelector(".crop-area");
        const img = ov.querySelector(".crop-img");
        const range = ov.querySelector("input");
        const ok = ov.querySelector(".crop-ok");
        let S = 0, base = 1, zoom = 1, x = 0, y = 0; // x,y = deslocamento do centro da foto em px de tela

        const aplicar = () => {
            const esc = base * zoom;
            const w = img.naturalWidth * esc, h = img.naturalHeight * esc;
            const maxX = Math.max(0, (w - S) / 2), maxY = Math.max(0, (h - S) / 2);
            x = Math.min(maxX, Math.max(-maxX, x));
            y = Math.min(maxY, Math.max(-maxY, y));
            img.style.width = w + "px";
            img.style.height = h + "px";
            img.style.transform = `translate(${-w / 2 + x}px, ${-h / 2 + y}px)`;
        };
        const definirZoom = (z) => { zoom = Math.min(4, Math.max(1, z)); range.value = zoom; aplicar(); };

        img.onload = () => {
            S = area.clientWidth;
            base = S / Math.min(img.naturalWidth, img.naturalHeight); // zoom 1 = foto cobrindo a moldura
            aplicar();
            ok.disabled = false;
        };
        img.onerror = () => fechar(null);
        img.src = url;

        range.addEventListener("input", () => definirZoom(parseFloat(range.value)));

        const ptrs = new Map();
        let distIni = 0, zoomIni = 1;
        area.addEventListener("pointerdown", (e) => {
            area.setPointerCapture(e.pointerId);
            ptrs.set(e.pointerId, { x: e.clientX, y: e.clientY });
            if (ptrs.size === 2) {
                const [a, b] = [...ptrs.values()];
                distIni = Math.hypot(a.x - b.x, a.y - b.y) || 1;
                zoomIni = zoom;
            }
        });
        area.addEventListener("pointermove", (e) => {
            if (!ptrs.has(e.pointerId)) return;
            const ant = ptrs.get(e.pointerId), nov = { x: e.clientX, y: e.clientY };
            ptrs.set(e.pointerId, nov);
            if (ptrs.size === 1) { x += nov.x - ant.x; y += nov.y - ant.y; aplicar(); }
            else if (ptrs.size === 2) {
                const [a, b] = [...ptrs.values()];
                definirZoom(zoomIni * Math.hypot(a.x - b.x, a.y - b.y) / distIni);
            }
        });
        const soltar = (e) => ptrs.delete(e.pointerId);
        area.addEventListener("pointerup", soltar);
        area.addEventListener("pointercancel", soltar);
        area.addEventListener("wheel", (e) => { e.preventDefault(); definirZoom(zoom * (e.deltaY < 0 ? 1.08 : 0.92)); }, { passive: false });

        function fechar(resultado) {
            URL.revokeObjectURL(url);
            ov.remove();
            resolve(resultado);
        }

        ov.querySelector(".crop-cancel").addEventListener("click", () => fechar(null));
        ok.addEventListener("click", () => {
            const esc = base * zoom;
            const w = img.naturalWidth * esc, h = img.naturalHeight * esc;
            // Moldura vai de -S/2 a S/2 em torno do centro; canto da foto está em (-w/2 + x, -h/2 + y).
            const sx = (-S / 2 - (-w / 2 + x)) / esc;
            const sy = (-S / 2 - (-h / 2 + y)) / esc;
            const sl = S / esc;
            const canvas = document.createElement("canvas");
            canvas.width = canvas.height = lado || 400;
            canvas.getContext("2d").drawImage(img, sx, sy, sl, sl, 0, 0, canvas.width, canvas.height);
            canvas.toBlob((b) => fechar(b), "image/jpeg", 0.9);
        });
    }),
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
