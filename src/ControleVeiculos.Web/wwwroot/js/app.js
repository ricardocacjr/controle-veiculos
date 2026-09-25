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
        let binario = "";
        const bytes = new Uint8Array(buffer);
        for (let i = 0; i < bytes.length; i += 0x8000) binario += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        return btoa(binario);
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
        parar: async () => {
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
        },
    };
})();
