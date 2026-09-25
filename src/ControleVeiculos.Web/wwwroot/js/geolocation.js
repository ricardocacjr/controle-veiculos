// Usado pela tela "Meu uso" pra capturar a localização do navegador (ver MeuUso.razor) ao
// enviar a foto do painel — resolve endereço de origem automaticamente por geocodificação
// reversa no servidor. Sempre devolve { lat, lng, error }: coordenadas quando dá certo, ou
// "error" preenchido com o motivo (contexto inseguro, sem suporte, permissão negada, timeout)
// pra mostrar pro motorista em vez de falhar em silêncio — sem isso não dava pra saber por que
// o endereço não vinha (ex: iOS Safari bloqueia geolocalização fora de HTTPS/localhost).
//
// No iPhone o GPS pedido logo depois de voltar da câmera costuma falhar (o navegador acabou de
// voltar do segundo plano). Por isso a tela da câmera "aquece" a localização antes (cvGeoAquecer)
// e a última posição boa fica guardada alguns minutos pra ser usada na hora da foto.
(() => {
    const VALIDADE_MS = 3 * 60 * 1000;
    let ultima = null;      // { lat, lng, t }
    let emAndamento = null; // Promise da consulta em curso (evita pedir duas vezes ao mesmo tempo)

    const navegador = /CriOS/.test(navigator.userAgent) ? "Chrome" : /FxiOS/.test(navigator.userAgent) ? "Firefox" : "Safari";
    const ehIphone = /iPhone|iPad|iPod/.test(navigator.userAgent);

    const explicar = (err) => {
        if (err.code === 1) {
            return ehIphone
                ? `o celular não deixou o app ver a localização. No iPhone: Ajustes › Privacidade e Segurança › Serviços de Localização › ${navegador === "Safari" ? "Sites do Safari" : navegador} › "Durante o Uso".`
                : "a permissão de localização foi negada. Toque no cadeado ao lado do endereço do site e permita a localização.";
        }
        if (err.code === 2) return "o GPS não achou sinal agora (confira se a Localização do celular está ligada).";
        return "a localização demorou demais.";
    };

    const consultar = () => new Promise((resolve) => {
        if (!window.isSecureContext) {
            resolve({ lat: null, lng: null, error: "Site não é HTTPS — Safari/Chrome bloqueiam localização fora de conexão segura." });
            return;
        }

        if (!navigator.geolocation) {
            resolve({ lat: null, lng: null, error: "Navegador não suporta geolocalização." });
            return;
        }

        // O "timeout" do getCurrentPosition só começa a contar DEPOIS que a pessoa responde ao
        // pedido de permissão — se o aviso ficar sem resposta, a promessa nunca terminava e a tela
        // ficava presa em "Lendo...". Esse limite próprio garante que sempre termina.
        let terminou = false;
        const fim = (r) => { if (!terminou) { terminou = true; resolve(r); } };
        setTimeout(() => fim({ lat: null, lng: null, error: `a localização demorou demais (confira se o ${navegador} tem permissão de localização).` }), 12000);

        navigator.geolocation.getCurrentPosition(
            pos => {
                ultima = { lat: pos.coords.latitude, lng: pos.coords.longitude, t: Date.now() };
                fim({ lat: ultima.lat, lng: ultima.lng, error: null });
            },
            err => fim({ lat: null, lng: null, error: explicar(err) }),
            { timeout: 10000, maximumAge: 120000, enableHighAccuracy: false }
        );
    });

    window.getGeolocation = () => {
        if (ultima && Date.now() - ultima.t < VALIDADE_MS)
            return Promise.resolve({ lat: ultima.lat, lng: ultima.lng, error: null });
        if (!emAndamento)
            emAndamento = consultar().finally(() => { emAndamento = null; });
        return emAndamento;
    };

    // Chamado quando aparece uma tela que vai precisar do local: pede a permissão e já guarda a posição.
    window.cvGeoAquecer = () => { window.getGeolocation(); };
})();

// Plano B do endereço: o Nominatim limita (HTTP 429) os IPs compartilhados de saída do Render,
// então quando a Api não consegue, o próprio celular consulta — com o IP dele, não o compartilhado.
// Mesmo formato do servidor: "Rua, número - Bairro, Cidade".
window.reverseGeocode = async (lat, lng) => {
    try {
        const url = `https://nominatim.openstreetmap.org/reverse?format=json&zoom=18&addressdetails=1&accept-language=pt-BR&lat=${lat}&lon=${lng}`;
        const resp = await fetch(url);
        if (!resp.ok) return null;
        const json = await resp.json();
        const a = json.address || {};
        if (!a.road) return json.display_name || null;
        const rua = a.house_number ? `${a.road}, ${a.house_number}` : a.road;
        const local = [a.suburb, a.city || a.town || a.village || a.municipality].filter(Boolean).join(", ");
        return local ? `${rua} - ${local}` : rua;
    } catch {
        return null;
    }
};
