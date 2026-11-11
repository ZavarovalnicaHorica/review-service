# review-service

Mikrostoritev za ročni pregled zahtevkov (ReviewService, Zavarovalnica Horica). Prejme `review.requested.v1`, naredi opravilo, zaposleni ga odobri, zavrne ali zahteva dopolnitev, nato se objavi `review.completed.v1`.

.NET 10, EF Core + PostgreSQL, Kafka, JWT (auth-service ali Keycloak).

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

Drugje je treba nastaviti `Auth__Authority` (auth-service, npr. `http://auth-service:8081`, ali URL Keycloak realma). Vlogi sta `reviewer` in `admin`; tokeni auth-service (`"roles": ["REVIEWER"]`) delujejo, v Helm values sta nastavljeni `REVIEWER` in `ADMIN`.

## Kubernetes (AKS)

Push na `main` požene teste, zgradi sliko `horica-review-service` in jo namesti v namespace `staging`; v `prod` gre z ročnim zagonom workflowa (Actions → CI/CD → Run workflow).

V namespaceu mora obstajati Secret `db-review` s ključem `DB_CONNECTION_STRING` (Npgsql), npr.:

    kubectl -n staging create secret generic db-review --from-literal=DB_CONNECTION_STRING="Host=<server>.postgres.database.azure.com;Port=5432;Database=review_staging;Username=<user>;Password=<password>;Ssl Mode=Require"

Baza se ustvari ob prvem zagonu, če jo uporabnik sme ustvariti. Kafka je na `horica-kafka-kafka-bootstrap.kafka:9092`; teme imajo v stagingu in prodi predpono (`staging.review.requested.v1`, `prod.review.requested.v1` ...), ker si okolji delita eno Kafko. Brez Kafke storitev vseeno teče, dogodki počakajo v outbox tabeli.

## Najpomembnejše nastavitve

`ConnectionStrings__ReviewDb`, `Kafka__BootstrapServers`, `Kafka__Enabled`, `Auth__Mode`, `Auth__Authority`. Vse je v `appsettings.json`, v okolju se preglasi z `__` namesto `:`.