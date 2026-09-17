// Usado pela tela "Meu uso" pra capturar a localização do navegador (ver MeuUso.razor) ao
// enviar a foto do painel — resolve endereço de origem automaticamente por geocodificação
// reversa no servidor. Retorna null se o navegador não suportar, o usuário negar a permissão,
// ou a localização não vier a tempo (timeout de 8s) — nesses casos o motorista preenche a
// origem manualmente, como já era antes.
window.getGeolocation = () => {
    return new Promise((resolve) => {
        if (!navigator.geolocation) {
            resolve(null);
            return;
        }

        navigator.geolocation.getCurrentPosition(
            pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
            () => resolve(null),
            { timeout: 8000, maximumAge: 60000 }
        );
    });
};
