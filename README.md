# review-service

Mikrostoritev za ročni pregled zahtevkov (ReviewService, Zavarovalnica Horica). Prejme `review.requested.v1`, naredi opravilo, zaposleni ga odobri, zavrne ali zahteva dopolnitev, nato se objavi `review.completed.v1`.

.NET 8, EF Core + PostgreSQL, Kafka, JWT (Keycloak).

## Zagon

    docker compose up --build

Swagger: http://localhost:5080/swagger (Postgres na 5433, Kafka na 9092). Tabele in teme se naredijo same.

Brez Dockerja za aplikacijo:

    docker compose up -d postgres kafka
    dotnet run --project src/ReviewService

Brez Kafke: `Kafka__Enabled=false`.

## Hiter preizkus

V Swaggerju (samo Development): `POST /dev/review-requests`, nato `GET /reviews?status=Pending`, `POST /reviews/{id}/assign`, `POST /reviews/{id}/decision`. Primeri klicev so v `src/ReviewService/requests.http`.

## API

- `GET /reviews` (filtri `status`, `claimId`, `mine`, `page`, `pageSize`)
- `GET /reviews/{id}`, `GET /reviews/{id}/history`
- `POST /reviews/{id}/assign`, `POST /reviews/{id}/release`
- `POST /reviews/{id}/decision` z `{"decision": "Approved|Rejected|NeedsMoreInfo", "reason": "..."}`. Razlog je obvezen pri Rejected in NeedsMoreInfo.
- `GET /health/live`, `GET /health/ready`

Za zahtevek je lahko odprto samo eno opravilo. Zaključenega opravila ni mogoče spremeniti (409).

## Dogodki

CloudEvents v JSON, ime teme je enako tipu dogodka, ključ je `claimId`.

**review.requested.v1** (vhod): obvezen je samo `claimId`. Ostalo je opcijsko: `policyId`, `customerId`, `claimedAmount`, `currency`, `riskScore`, `riskReasons`, `recommendation`, `summary`. Sprejme ovojnico ali samo `data`. Neberljiva sporočila gredo v `review.requested.v1.dlq`.

**review.completed.v1** (izhod), `data`:

    { "reviewId": "...", "claimId": "CLM-2001", "decision": "Approved",
      "reason": "...", "reviewerId": "...", "reviewerName": "ana", "decidedAt": "..." }

Odločitev in dogodek se shranita v isti transakciji (outbox), dostava je vsaj enkrat, zato naj prejemnik dedupicira po `id`. Primera sta v `samples/`.

## Prijava

V Development je prijava izklopljena (`Auth__Mode=Disabled`, uporabnik `dev-reviewer`). Drugega uporabnika simuliraš z glavama `X-Dev-User` in `X-Dev-Roles`.

Drugje je treba nastaviti `Auth__Authority` (URL Keycloak realma). Vlogi sta `reviewer` in `admin`.

## Najpomembnejše nastavitve

`ConnectionStrings__ReviewDb`, `Kafka__BootstrapServers`, `Kafka__Enabled`, `Auth__Mode`, `Auth__Authority`. Vse je v `appsettings.json`, v okolju se preglasi z `__` namesto `:`.