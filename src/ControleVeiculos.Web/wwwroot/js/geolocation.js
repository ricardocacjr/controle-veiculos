// Usado pela tela "Meu uso" pra capturar a localização do navegador (ver MeuUso.razor) ao
// enviar a foto do painel — resolve endereço de origem automaticamente por geocodificação
// reversa no servidor. Sempre devolve { lat, lng, error }: coordenadas quando dá certo, ou
// "error" preenchido com o motivo (contexto inseguro, sem suporte, permissão negada, timeout)
// pra mostrar pro motorista em vez de falhar em silêncio — sem isso não dava pra saber por que
// o endereço não vinha (ex: iOS Safari bloqueia geolocalização fora de HTTPS/localhost).
window.getGeolocation = () => {
    return new Promise((resolve) => {
        if (!window.isSecureContext) {
            resolve({ lat: null, lng: null, error: "Site não é HTTPS — Safari/Chrome bloqueiam localização fora de conexão segura." });
            return;
        }

        if (!navigator.geolocation) {
            resolve({ lat: null, lng: null, error: "Navegador não suporta geolocalização." });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude, error: null }),
            err => resolve({ lat: null, lng: null, error: `Geolocalização falhou: ${err.message} (código ${err.code}).` }),
            { timeout: 8000, maximumAge: 60000 }
        );
    });
};

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
