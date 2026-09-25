# ControleVeiculos

App de controle de uso de frota para empresas: registro de saída/retorno de veículo (odômetro,
finalidade, fotos e nota de voz transcrita) e abastecimento. Dois clientes sobre a mesma Api:
**app mobile** (motoristas, registram o uso em campo) e **painel web** (gestores, acompanham
veículos, motoristas e histórico de uso). Stack: C# / .NET 8, mesma arquitetura do projeto
[SuporteRemoto](../Projeto%20Suporte%20Remoto).

## Arquitetura

```
ControleVeiculos.sln
/src
  ControleVeiculos.Domain          -> Entidades e enums (Vehicle, Driver, UsageRecord, ...)
  ControleVeiculos.Application     -> Interfaces de repositório e serviços (casos de uso)
  ControleVeiculos.Infrastructure  -> EF Core (MySQL/Pomelo), ASP.NET Core Identity, repositórios,
                                       transcrição de voz (Google Cloud Speech-to-Text), leitura
                                       de odômetro e de comprovante de abastecimento por OCR
                                       (Google Cloud Vision), geocodificação reversa (Nominatim)
  ControleVeiculos.Shared          -> DTOs/contratos usados pela Api, Web e Mobile
  ControleVeiculos.Api             -> ASP.NET Core Web API (auth JWT, veículos, motoristas, usos)
  ControleVeiculos.Web             -> Blazor Server (painel do gestor)
  ControleVeiculos.Mobile          -> App .NET MAUI (Android/iOS/Windows) do motorista
```

Entidades principais (`Vehicle`, `Driver`, `UsageRecord`, ...) já têm `TenantId` (nullable) para
não travar uma futura oferta multi-tenant, sem implementar isso agora — mesmo padrão do
SuporteRemoto.

Duas entidades de apoio ao uso, com naturezas bem diferentes:
- **`Empresa`** (Voglio, OAK, Uso Pessoal, ...): cadastro gerenciado à parte, igual veículo —
  só Admin/Gestor cria/edita/remove (`/empresas` no painel, `EmpresasController` na Api).
- **`MotivoUso`** (finalidade do trajeto): catálogo que **cresce sozinho** — toda vez que um uso é
  iniciado com uma finalidade inédita, ela entra automaticamente no catálogo
  (`IMotivoUsoRepository.EnsureExistsAsync`, chamado em `UsageRecordsController.Start`) e passa a
  aparecer como sugestão (`<datalist>`) da próxima vez. Não tem tela de cadastro nem tela de
  edição — só decorre do uso normal do app.

### Fluxo de uso

1. Motorista abre o app (mobile ou a tela `/motorista/uso` do painel web), entra com login curto
   ou e-mail, e **antes de iniciar o uso** pode tirar uma foto do painel do veículo
   (`POST /api/usagerecords/ler-painel`) — lê o odômetro por OCR e, se o navegador/app compartilhar
   a localização, resolve o endereço por geocodificação reversa, pré-preenchendo odômetro inicial
   e origem (ainda editáveis).
2. Confirma os campos — veículo, empresa (opcional, lista fixa), finalidade (digita livre, com
   sugestões do catálogo que cresce sozinho) — e inicia o uso (`POST /api/usagerecords/iniciar`)
   — o veículo passa a `EmUso`.
3. Durante o uso: fotos (odômetro — com leitura automática sugerida por OCR, avarias), notas de
   voz (transcritas automaticamente) e abastecimentos podem ser anexados ao registro em andamento.
   Abastecimento também aceita uma foto da nota fiscal/cupom
   (`POST /api/usagerecords/{id}/abastecimentos/ler-comprovante`), que sugere litros, valor total
   e valor por litro.
4. Ao devolver o veículo, informa o odômetro final (`POST /api/usagerecords/{id}/finalizar`) — o
   veículo volta a `Disponivel` e o odômetro do veículo é atualizado.
5. O gestor acompanha tudo isso pelo painel web (`/veiculos`, `/motoristas`, `/usos`).

Em todos os casos de leitura por foto (odômetro, painel, comprovante), o valor lido é sempre uma
**sugestão pré-preenchida, nunca aplicada sem confirmação** — o motorista/gestor ainda revisa e
pode corrigir antes de salvar.

## Ambiente de produção (Render + TiDB Cloud)

A Api e o painel Web estão publicados e acessíveis pela internet:
- Api: https://controle-veiculos-api-v3l0.onrender.com (`/health` consulta o banco)
- Web: https://controle-veiculos-web-hqyg.onrender.com

Deploy via [Render](https://render.com) (Web Services em Docker, build a partir de
[`src/ControleVeiculos.Api/Dockerfile`](src/ControleVeiculos.Api/Dockerfile) e
[`src/ControleVeiculos.Web/Dockerfile`](src/ControleVeiculos.Web/Dockerfile)) + banco
[TiDB Cloud](https://tidbcloud.com) Starter (grátis, compatível com MySQL, região Oregon), banco
`controle_veiculos`. Começou no Aiven MySQL free (compartilhado com o SuporteRemoto), mas o Aiven
**desliga** serviços grátis inativos e precisa ser religado à mão — a Api ficava em loop de queda.
O TiDB Starter é serverless (sem VM pra desligar) e não tem pausa manual; o plano grátis tem 5 GiB
e 50M Request Units por mês.

**Compatibilidade TiDB**: o Pomelo cria colunas `Guid` com collation `ascii_general_ci`, que o TiDB
recusa ("Unsupported collation when new collation is enabled"). Por isso o modelo define
`UseGuidCollation("utf8mb4_bin")` e as migrations foram regeneradas numa só (`InitialCreate`). A
versão do servidor é fixa no código (sem `AutoDetect`, que exigiria banco acessível na
inicialização), e `SKIP_DB_INIT=1` deixa gerar migrations sem banco:
`SKIP_DB_INIT=1 dotnet ef migrations add <Nome> --project src/ControleVeiculos.Infrastructure --startup-project src/ControleVeiculos.Api --output-dir Persistence/Migrations`.

**Mantendo acordado**: o Render grátis dorme após ~15 min sem requisição. Um MikroTik (24h ligado)
chama `/health` da Api e a página do Web a cada 10 min via `/system scheduler` + `/tool fetch`.

Segredos (connection string, `Jwt:Key`) ficam só nas
variáveis de ambiente do Render; a credencial do Google Cloud (Speech-to-Text + Vision) é um
"Secret File" do Render, montado em `/etc/secrets/google-credentials.json` — nada disso é
versionado.

Motivo do deploy: o app usa geolocalização do navegador (`navigator.geolocation`) pra preencher a
origem automaticamente ao registrar saída, e o iOS Safari bloqueia essa API fora de um contexto
seguro (HTTPS). Rodando local por IP/HTTP simples esse recurso falhava silenciosamente — o Render
resolve isso de vez, já que serve tudo com TLS.

A base de produção foi populada à parte do banco local de desenvolvimento: veículo (`XYZ9A88`),
empresas (Voglio/OAK/Uso Pessoal), motivos e os motoristas DANI/ERICK/JONATHAN/JOSLEY/RICARDO/GIL.

### Entrada no app (motoristas)

Tela de perfis com foto (estilo Netflix) → **PIN de 6 números**. O Admin cadastra o motorista em
`/motoristas` com um PIN temporário (padrão `123456`); no primeiro acesso o app obriga a criar o
PIN próprio e oferece tirar uma selfie pro perfil. A sessão fica lembrada no aparelho
(`localStorage`, token JWT de 30 dias — `Jwt__ExpirationMinutes=43200` no Render), então abrir o
app pelo ícone da tela de início já cai direto no uso. "Esqueci o PIN" = o Admin gera outro
temporário (🔒 na lista de motoristas). Cadastro de contas é **só pelo Admin** (antes o
`/api/auth/register` era aberto). Em banco novo, `BootstrapAdmin__Email`/`BootstrapAdmin__Senha`
criam o primeiro Admin. Admin/gestor sem perfil de motorista entram por "Acesso do administrador"
(e-mail + senha). RICARDO é motorista e Admin.

### Fluxo do motorista

Sem menu: logou → **Nova saída** em 4 passos (empresa → motivo → foto do painel → SAIR). A foto
do painel preenche o km (Google Vision, usando o último km do carro como referência pra ignorar
relógio/autonomia) e a origem (GPS; perto da base vira "Base"). Não existe destino: toda saída
começa e termina na **base** (seção `Base` do `appsettings.json` do Web — Rua Baldur Magnus
Grubba, 2939, raio de 300 m). **Em uso**, três botões: *Abasteci* (foto do cupom → litros/valor;
foto do painel → km; local vira ponto no mapa), *Tive um problema* (foto, ou áudio gravado no
próprio app em WAV 16 kHz — o iPhone grava AAC, que o Google Speech v1 não aceita) e *Cheguei*
(foto do painel **obrigatória**, validada no servidor; avisa se não estiver na base). Link
discreto "Minhas saídas" pro histórico.

Sem o ping, a primeira requisição depois de o Render dormir volta 502 por alguns segundos; o login
do painel tenta de novo sozinho e, se ainda falhar, avisa que o servidor está iniciando.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Workload MAUI (`dotnet workload install maui`) — necessário só para compilar/rodar o app mobile
- MySQL Server (provider [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql))
- Ferramenta `dotnet-ef` (`dotnet tool install --global dotnet-ef`)
- (Opcional) Conta Google Cloud com as APIs "Cloud Speech-to-Text" e "Cloud Vision" habilitadas,
  para transcrição real das notas de voz e leitura automática do odômetro — ver seção abaixo

## Banco de dados (MySQL)

Usa a mesma instalação local de MySQL do projeto SuporteRemoto (sem privilégios de admin, por
isso não roda como serviço do Windows — precisa ser iniciado manualmente a cada reinício):

```bash
"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqld.exe" --basedir="C:\Program Files\MySQL\MySQL Server 8.4" --datadir="C:\Users\Usuario\mysql-data" --port=3306
```

Banco e usuário já criados:
- Database: `controle_veiculos_db`
- Usuário da aplicação: `controle_veiculos_app` / senha `ControleVeiculos_App_2026!`
- Root: senha `SuporteRemoto_Root_2026!` (mesma conta root do MySQL compartilhado com o
  SuporteRemoto)

A connection string está em
[`src/ControleVeiculos.Api/appsettings.Development.json`](src/ControleVeiculos.Api/appsettings.Development.json).
Migrations já aplicadas nesta máquina; para reaplicar em outro ambiente:

```bash
dotnet ef database update --project src/ControleVeiculos.Infrastructure --startup-project src/ControleVeiculos.Api
```

A migration inicial (`InitialCreate`) já está no repositório — a
[`AppDbContextFactory`](src/ControleVeiculos.Infrastructure/Persistence/AppDbContextFactory.cs)
de design-time usada pelo `dotnet ef` tem prioridade sobre `--startup-project`, então suas
credenciais precisam bater com o banco/usuário acima (se mudar a senha local, atualize os dois
lugares).

## Configuração de voz e imagem (Google Cloud)

Transcrição de nota de voz e leitura automática de odômetro usam a **mesma conta de serviço** do
Google Cloud — uma única credencial cobre os dois.

1. Crie um projeto no [Google Cloud Console](https://console.cloud.google.com) (exige cartão de
   crédito pra ativar faturamento, mesmo usando só a faixa gratuita).
2. Habilite as APIs **"Cloud Speech-to-Text API"** e **"Cloud Vision API"** nesse projeto
   ([APIs e Serviços → Biblioteca](https://console.cloud.google.com/apis/library)).
3. Crie uma conta de serviço (IAM e administrador → Contas de serviço → Criar conta de serviço).
   Papel "Editor" no projeto é suficiente pra essa fase inicial (dá pra restringir depois pra
   "Cloud Speech Client" + acesso à Vision API especificamente).
4. Na conta de serviço criada, gere uma chave JSON ("Chaves" → "Adicionar chave" → "Criar nova
   chave" → JSON) e baixe o arquivo.
5. Aponte `GoogleCloud:CredentialsPath` em `appsettings.Development.json` (local) ou a variável de
   ambiente `GOOGLE_APPLICATION_CREDENTIALS` (produção) para o caminho desse arquivo — **nunca
   commite esse JSON** (o `.gitignore` já bloqueia `GoogleCloud-credentials*.json`, mas confira se
   o caminho que você usar cai fora do repositório ou segue esse padrão de nome).

**Sem essa configuração o app funciona normalmente** — upload de foto/nota de voz continua
funcionando, só que a transcrição fica marcada como `Falhou` (`VoiceNoteStatus`) e as fotos de
odômetro/comprovante não vêm com leitura sugerida (campos ficam `null`).

A **geocodificação reversa** (endereço a partir de latitude/longitude) usa
[Nominatim](https://nominatim.openstreetmap.org) (OpenStreetMap) — gratuito, sem conta nem chave
de API, diferente dos serviços de voz/imagem. Único cuidado é não abusar da taxa de uso (a
política pede no máximo ~1 requisição/segundo; o uso deste app fica bem abaixo disso).

⚠️ **Limitações atuais**:
- Voz: reconhecimento síncrono (`Recognize`), só suporta áudios de até ~1 minuto — notas mais
  longas vão falhar. Trocar para `LongRunningRecognize` se isso virar um problema real.
- Odômetro: a leitura por OCR (`GoogleVisionOdometerOcrService`) usa uma heurística simples — pega
  a sequência de dígitos mais longa detectada na foto (3 a 7 dígitos). Funciona bem pra odômetros
  digitais nítidos, mas pode errar com reflexo no vidro, ângulo ruim ou painéis analógicos.
- Comprovante de abastecimento: a leitura (`GoogleVisionFuelReceiptOcrService`) procura
  litros/valor unitário/valor total perto de palavras-chave ("LITRO", "UNIT"/"PREÇO", "TOTAL"),
  olhando a linha da palavra-chave e as duas seguintes (cupons costumam quebrar rótulo e valor em
  linhas diferentes). Layout de cupom fiscal varia muito no Brasil — funciona bem nos formatos mais
  comuns, mas não é garantido pra todo layout.
- Em todos os casos acima: é sempre **sugestão pra conferência humana** (pré-preenche o campo, mas
  o motorista/gestor confirma antes de salvar), nunca aplicada direto sem revisão.

## Rodando localmente

```bash
# Api (porta 5044) — Swagger em /swagger, aplica seed dos papéis (Admin, Gestor, Motorista)
dotnet run --project src/ControleVeiculos.Api

# Web (Blazor Server, painel do gestor) — aponta para a Api via appsettings (ApiBaseUrl)
dotnet run --project src/ControleVeiculos.Web

# Mobile (Windows, mais rápido pra testar sem emulador Android)
dotnet build src/ControleVeiculos.Mobile -f net8.0-windows10.0.19041.0
dotnet run --project src/ControleVeiculos.Mobile -f net8.0-windows10.0.19041.0
```

Para rodar o app mobile em Android, é necessário instalar o Android SDK (via Visual Studio
Installer, componente ".NET Multi-platform App UI development", ou Android Studio) — não incluído
neste ambiente. Depois de instalado: `dotnet build src/ControleVeiculos.Mobile -f net8.0-android`.

O app mobile aponta para a Api via
[`Services/AppConfig.cs`](src/ControleVeiculos.Mobile/Services/AppConfig.cs) —
`10.0.2.2` é o alias do emulador Android para o `localhost` da máquina host; em dispositivo físico
troque pelo IP da máquina na rede local, ou pela URL de produção depois do deploy.

## Papéis

- **Admin** / **Gestor**: cadastram veículos e motoristas, veem todos os usos registrados pelo
  painel web.
- **Motorista**: usa o app mobile para iniciar/finalizar uso de veículo e anexar fotos, notas de
  voz e abastecimentos.

## Limitações conhecidas (fase inicial, propositalmente simples)

Seguindo o mesmo raciocínio do SuporteRemoto — simples agora, gaps documentados em vez de
escondidos:

1. **Cadastro sem restrição de papel**: `POST /api/auth/register` deixa qualquer um se cadastrar
   como Admin, Gestor ou Motorista sem aprovação. Antes de uso real, precisa de convite/aprovação
   manual para papéis de gestão.
2. **Motorista não fica automaticamente vinculado ao seu login**: `Driver.UserId` existe no
   modelo, mas a tela de cadastro de motorista no painel web não faz esse vínculo ainda — hoje
   isso exigiria criar o motorista via Api passando o `UserId` do usuário já cadastrado. Sem esse
   vínculo, o app mobile não encontra o `Driver` do usuário logado.
3. **Fotos e notas de voz em disco local**: salvas em `App_Data/` dentro do container/máquina —
   efêmero em PaaS como Render (some a cada redeploy). Decisão deliberada, não pendência: o
   usuário confirmou que não precisa guardar foto/áudio depois do uso, então isso ficou de fora
   do escopo do deploy em produção.
4. **Transcrição de voz síncrona**: ver limitação de ~1 minuto acima.
5. **App mobile não testado em dispositivo real**: foi validado compilando para Windows (sem
   Android SDK neste ambiente) — os fluxos de câmera/microfone/permissões foram implementados
   seguindo a documentação do MAUI e do Plugin.Maui.Audio, mas precisam de um teste ponta a ponta
   em Android/iOS de verdade antes de considerar o módulo pronto.

## Próximos passos (fora desta etapa)

1. Vincular motorista ↔ usuário logado direto na tela de cadastro do painel web.
2. Testar o app mobile em um dispositivo/emulador Android real (câmera, microfone, upload).
3. Fechar a simplificação de cadastro sem aprovação (item 1 das limitações acima).
4. Apontar o app mobile (`AppConfig`) pra URL de produção da Api em vez de `localhost`/`10.0.2.2`.
