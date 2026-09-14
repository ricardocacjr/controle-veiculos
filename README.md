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
                                       transcrição de voz (Google Cloud Speech-to-Text)
  ControleVeiculos.Shared          -> DTOs/contratos usados pela Api, Web e Mobile
  ControleVeiculos.Api             -> ASP.NET Core Web API (auth JWT, veículos, motoristas, usos)
  ControleVeiculos.Web             -> Blazor Server (painel do gestor)
  ControleVeiculos.Mobile          -> App .NET MAUI (Android/iOS/Windows) do motorista
```

Entidades principais (`Vehicle`, `Driver`, `UsageRecord`, ...) já têm `TenantId` (nullable) para
não travar uma futura oferta multi-tenant, sem implementar isso agora — mesmo padrão do
SuporteRemoto.

### Fluxo de uso

1. Motorista abre o app mobile, entra com e-mail/senha, escolhe um veículo disponível e informa
   finalidade + odômetro inicial (`POST /api/usagerecords/iniciar`) — o veículo passa a
   `EmUso`.
2. Durante o uso: fotos (odômetro, avarias), notas de voz (transcritas automaticamente) e
   abastecimentos podem ser anexados ao registro em andamento.
3. Ao devolver o veículo, informa o odômetro final (`POST /api/usagerecords/{id}/finalizar`) — o
   veículo volta a `Disponivel` e o odômetro do veículo é atualizado.
4. O gestor acompanha tudo isso pelo painel web (`/veiculos`, `/motoristas`, `/usos`).

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Workload MAUI (`dotnet workload install maui`) — necessário só para compilar/rodar o app mobile
- MySQL Server (provider [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql))
- Ferramenta `dotnet-ef` (`dotnet tool install --global dotnet-ef`)
- (Opcional) Conta Google Cloud com a API "Cloud Speech-to-Text" habilitada, para transcrição real
  das notas de voz — ver seção abaixo

## Banco de dados (MySQL)

Ainda não há um MySQL rodando localmente nesta máquina para este projeto (diferente do
SuporteRemoto, que já tinha um instalado) — suba um MySQL 8.x e ajuste
[`src/ControleVeiculos.Api/appsettings.Development.json`](src/ControleVeiculos.Api/appsettings.Development.json)
se as credenciais abaixo não corresponderem ao seu ambiente:

- Database: `controle_veiculos_db`
- Usuário: `controle_veiculos_app` / senha `ControleVeiculos_App_2026!`

Para aplicar as migrations:

```bash
dotnet ef database update --project src/ControleVeiculos.Infrastructure --startup-project src/ControleVeiculos.Api
```

A migration inicial (`InitialCreate`) já está no repositório — foi gerada com uma
[`AppDbContextFactory`](src/ControleVeiculos.Infrastructure/Persistence/AppDbContextFactory.cs)
de design-time (versão fixa do MySQL) porque no ambiente onde o projeto foi criado não havia um
servidor MySQL acessível para o `ServerVersion.AutoDetect` usado em runtime.

## Configuração da transcrição de voz (Google Cloud Speech-to-Text)

1. Crie um projeto no [Google Cloud Console](https://console.cloud.google.com), habilite a API
   "Cloud Speech-to-Text" e crie uma conta de serviço com a role "Cloud Speech Client".
2. Baixe o JSON de credenciais da conta de serviço.
3. Aponte `GoogleCloud:CredentialsPath` em `appsettings.Development.json` (local) ou a variável de
   ambiente `GOOGLE_APPLICATION_CREDENTIALS` (produção) para o caminho desse arquivo.

**Sem essa configuração o app funciona normalmente** — o upload de nota de voz é salvo, só que a
transcrição fica marcada como `Falhou` (`VoiceNoteStatus`) em vez de `Transcrito`, e pode ser
reprocessada depois.

⚠️ **Limitação atual**: usa reconhecimento síncrono (`Recognize`), que só suporta áudios de até
~1 minuto — suficiente para notas curtas, mas notas mais longas vão falhar. Trocar para
`LongRunningRecognize` se isso virar um problema real.

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
   efêmero em PaaS como Render (some a cada redeploy). Trocar por armazenamento externo
   (S3-compatível) antes de qualquer deploy real.
4. **Transcrição de voz síncrona**: ver limitação de ~1 minuto acima.
5. **App mobile não testado em dispositivo real**: foi validado compilando para Windows (sem
   Android SDK neste ambiente) — os fluxos de câmera/microfone/permissões foram implementados
   seguindo a documentação do MAUI e do Plugin.Maui.Audio, mas precisam de um teste ponta a ponta
   em Android/iOS de verdade antes de considerar o módulo pronto.

## Próximos passos (fora desta etapa)

1. Vincular motorista ↔ usuário logado direto na tela de cadastro do painel web.
2. Testar o app mobile em um dispositivo/emulador Android real (câmera, microfone, upload).
3. Trocar armazenamento de fotos/áudio local por um provedor externo (S3-compatível).
4. Fechar a simplificação de cadastro sem aprovação (item 1 das limitações acima).
5. Deploy em produção (Render + Aiven, mesmo padrão do SuporteRemoto) quando o fluxo estiver
   validado localmente.
