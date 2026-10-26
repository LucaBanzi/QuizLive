# QuizLive

Web app per serate quiz dal vivo (10–30 tavoli, dimensionata fino a 60).
Tre viste: **Giocatore** `/` (mobile), **Display** `/display` (schermo/proiettore), **Admin** `/admin` (regia).

- Backend: ASP.NET Core 8 + SignalR + EF Core (PostgreSQL), server autoritativo
- Frontend: React + TypeScript + Vite + Tailwind + zustand, servito da nginx
- Esposizione: nginx (porta host 8080) ← Tailscale Funnel (HTTPS)

## Avvio rapido

```bash
cp .env.example .env        # poi modifica POSTGRES_PASSWORD, ADMIN_PASSWORD, JWT_SECRET (>= 32 caratteri)
docker compose up -d --build
curl http://127.0.0.1:8080/healthz
```

1. Apri `http://localhost:8080/admin` → password = `ADMIN_PASSWORD`.
2. In **Domande** crea le domande (oppure *Importa JSON* con `sample-questions.json`).
3. Apri `http://localhost:8080/display` sul laptop collegato al proiettore e clicca **Avvia Display** (sblocca audio/video).
4. I giocatori aprono l'URL pubblico (QR sul Display), iscrivono il tavolo e si parte dalla **Regia**.

Flusso di ogni domanda (pulsanti della Regia, abilitati solo se coerenti con la fase):
`Prossima domanda → Mostra domanda → Rileva risposte → Chiudi risposte → Mostra risposta corretta → Mostra classifica`

## Tailscale Funnel (sul laptop Ubuntu, Tailscale installato sull'host)

1. `curl -fsSL https://tailscale.com/install.sh | sh` poi `sudo tailscale up`.
2. Console Tailscale: abilita **MagicDNS** e **HTTPS Certificates** (scheda DNS); nelle ACL consenti Funnel con un blocco
   `nodeAttrs` (target `autogroup:member`, attr `funnel`). Il pulsante compare anche alla prima esecuzione del comando.
3. `docker compose up -d --build`
4. `sudo tailscale funnel --bg 8080` → stampa l'URL `https://NOMEMACCHINA.TAILNET.ts.net`
5. Metti l'URL in `PUBLIC_ORIGIN` nel `.env` e rilancia `docker compose up -d` (serve al QR del Display).
6. Verifica: `tailscale funnel status` e apri l'URL da uno smartphone in rete mobile.
7. A fine serata: `sudo tailscale funnel --bg 8080 off` (oppure `tailscale funnel reset`).

Note: apri il Display su `http://localhost:8080/display` (non sull'URL pubblico) così video e audio non consumano banda.
Fallback senza Internet: pubblica la porta come `8080:80` in `docker-compose.yml` e collega i telefoni alla stessa Wi-Fi (`http://IP-LAN:8080`).
Il pannello `/admin` è raggiungibile da Internet: usa una password forte (login con rate limit 5/min per IP).

Backup: `docker compose exec db pg_dump -U quiz quizlive > backup.sql`

## Sviluppo locale

```bash
# database
docker run -d --name quiz-pg -e POSTGRES_DB=quizlive -e POSTGRES_USER=quiz -e POSTGRES_PASSWORD=quiz -p 5432:5432 postgres:16-alpine

# backend (porta 8080)
cd backend/src/QuizLive.Api
export Admin__Password=admin123 Jwt__Secret=una-stringa-casuale-di-almeno-32-caratteri ASPNETCORE_URLS=http://localhost:8080
dotnet run

# frontend (porta 5173, proxy verso il backend)
cd frontend && npm install && npm run dev
```

## Test

```bash
cd backend && dotnet test
```

Coprono: macchina a stati, calcolo punteggio e rank (1, 2, 2, 4), assenza di `CorrectLabel` nei payload pubblici
prima del reveal, invio unico/definitivo della risposta, Unlock/Rejoin, Transfer con risoluzione dei conflitti, reset.

## Migrazioni EF Core

Il repository non contiene file di migrazione (vanno generati con gli strumenti .NET). Finché non esistono, all'avvio lo schema
viene creato con `EnsureCreated`. Per usare le migrazioni (consigliato per aggiornamenti futuri dello schema), **prima del primo avvio**:

```bash
dotnet tool install --global dotnet-ef
cd backend/src/QuizLive.Api
dotnet ef migrations add Initial -o Data/Migrations
```

Da quel momento `Program.cs` applica `MigrateAsync()` all'avvio. Se avevi già avviato con `EnsureCreated`, azzera il volume
(`docker compose down -v`) prima di passare alle migrazioni.

## Scelte dove il documento di specifica lasciava libertà

- **Admin**: username fisso `admin`; la password viene dal `.env`. Se cambia, il nuovo valore diventa effettivo al riavvio.
- **Lobby**: `GameStateDto.Leaderboard` è valorizzato anche in Lobby (con punteggi a zero) perché il Display mostri l'elenco dei tavoli iscritti.
- **Rileva risposte**: `OpenAnswers` cancella eventuali risposte residue della domanda (utile se la domanda viene riproposta con `GoToQuestion`).
- **Rejoin**: nickname inesistente o tavolo disattivato → `NOT_AUTHORIZED`; senza token e finestra chiusa → `RECOVERY_CLOSED`.
- **Riattivare un tavolo** non riapre il recupero: serve poi *Sblocca* per ricollegare il telefono.
- **Fine domande**: se `NextQuestion` non trova altre domande la serata passa a `Finished`.
- **Seq**: viene incrementato a ogni modifica di stato (transizioni, iscrizioni, risposte accettate, correzioni); i client scartano snapshot con `Seq` inferiore.
- **CSV**: separatore `;`, UTF-8 con BOM, celle che iniziano con `= + - @` neutralizzate per Excel.
- **Riordino domande**: drag-and-drop HTML5 nativo (nessuna libreria aggiuntiva).

## Struttura

```
quiz-live/
├── docker-compose.yml  .env.example  sample-questions.json
├── backend/  (QuizLive.sln, Dockerfile)
│   ├── src/QuizLive.Api/   Domain · Data · Hubs · Services · Controllers · Dtos · Program.cs
│   └── tests/QuizLive.Tests/
└── frontend/ (Dockerfile, nginx.conf, Vite + React)
    └── src/  api · realtime · store · pages/{player,display,admin} · components · lib
```

Il volume media (`media`) è un volume Docker montato in `/app/media`, non è nel repository.
