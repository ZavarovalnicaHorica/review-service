# Horica ReviewService

Mikrostoritev **6. ReviewService – pregled zaposlenega (človek v zanki)** za projekt Zavarovalnica Horica.

Kaj dela:

- prejme dogodek `review.requested.v1` (objavi ga ClaimsService) in ustvari **opravilo pregleda**,
- zaposleni opravila vidi v čakalni vrsti, jih prevzame in **odobri, zavrne ali zahteva dopolnitev** (UC-6),
- razlog in čas odločitve se shranita, vsaka sprememba se zapiše v **zgodovino** (UC-8),
- po odločitvi objavi dogodek `review.completed.v1` (CloudEvents), na katerega se odzove ClaimsService.

Tehnologije: .NET 8 (C#), ASP.NET Core, EF Core + PostgreSQL, Apache Kafka (Confluent.Kafka), OIDC/JWT (Keycloak), Swagger, Docker.

---

## 1. Hiter zagon (Docker Compose)

Potrebuješ samo Docker.

```bash
docker compose up --build
```

Ko se vse zažene (prvi zagon traja nekaj minut zaradi prenosa slik):

| Kaj | Naslov |
|---|---|
| Swagger UI | http://localhost:5080/swagger |
| Zdravje (baza) | http://localhost:5080/health/ready |
| PostgreSQL | `localhost:5433`, uporabnik `postgres`, geslo `postgres`, baza `review_db` |
| Kafka (z računalnika) | `localhost:9092` |

Tabele v bazi in Kafka teme (`review.requested.v1`, `review.completed.v1`, `review.requested.v1.dlq`) se ustvarijo samodejno.

### Zagon brez Dockerja za aplikacijo (Visual Studio / VS Code)

Infrastrukturo poganjaš v Dockerju, aplikacijo lokalno:

```bash
docker compose up -d postgres kafka
dotnet run --project src/ReviewService
```

Odpre se `http://localhost:5080/swagger` (profil `http` v `launchSettings.json` nastavi okolje `Development`).
V Visual Studiu odpri `ReviewService.sln` in zaženi projekt `ReviewService`.

Potrebuješ .NET 8 SDK ali novejši (projekt cilja `net8.0` in se zažene tudi na .NET 9/10 runtime).

---

## 2. Preizkus celotnega toka (UC-6)

### A) Brez Kafke (najhitreje, samo Development)

V Swaggerju (ali v `src/ReviewService/requests.http`):

1. `POST /dev/review-requests` – ustvari opravilo (enako, kot da bi prišel `review.requested.v1`).
2. `GET /reviews?status=Pending` – čakalna vrsta.
3. `POST /reviews/{id}/assign` – prevzem opravila.
4. `POST /reviews/{id}/decision` z `{"decision":"Approved","reason":"..."}`.
5. `GET /reviews/{id}/history` – zgodovina.

### B) S Kafko

Pošlji vzorčni dogodek (ključ `CLM-2001`, vrednost CloudEvent):

```bash
# bash / cmd
docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic review.requested.v1 --property parse.key=true --property "key.separator=|" < samples/review-requested.kafka.txt
```

```powershell
# PowerShell
Get-Content samples/review-requested.kafka.txt | docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic review.requested.v1 --property parse.key=true --property "key.separator=|"
```

Nato v Swaggerju opravilo odobriš ali zavrneš in v drugem oknu vidiš dogodek:

```bash
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic review.completed.v1 --from-beginning --property print.key=true
```

---

## 3. REST API

Vsi klici zahtevajo vlogo `reviewer` (ali `admin`). Napake so v obliki RFC 7807 (`application/problem+json`).

| Metoda in pot | Opis |
|---|---|
| `GET /reviews?status=&claimId=&mine=&page=&pageSize=` | Seznam opravil. `status`: `Pending`, `InProgress`, `Completed`. Odprta opravila so urejena od najstarejšega (vrsta), ostala od najnovejšega. `pageSize` največ 100. |
| `GET /reviews/{id}` | Podrobnosti: znesek, ocena tveganja, razlogi, povzetek in predlog agenta, status, odločitev. |
| `GET /reviews/{id}/history` | Zgodovina (Created, Refreshed, Assigned, Released, Decided). |
| `POST /reviews/{id}/assign` | Prevzem opravila. Če je že dodeljeno drugemu zaposlenemu, vrne 409 (razen za `admin`). |
| `POST /reviews/{id}/release` | Vrne opravilo v vrsto (lahko ga sprosti dodeljeni zaposleni ali `admin`). |
| `POST /reviews/{id}/decision` | Telo: `{"decision":"Approved\|Rejected\|NeedsMoreInfo","reason":"..."}`. `reason` je obvezen pri `Rejected` in `NeedsMoreInfo`. |
| `GET /health/live`, `GET /health/ready` | Probe (živost, pripravljenost = baza dosegljiva). |
| `POST /dev/review-requests` | Samo v okolju `Development`: ustvari opravilo brez Kafke (drugje vrne 404). |

Pravila:

- Za isti zahtevek je lahko hkrati **samo eno odprto opravilo**. Če pride nov `review.requested.v1` za zahtevek z odprtim opravilom, se podatki opravila osvežijo (v zgodovini `Refreshed`). Po zaključenem pregledu (npr. po dopolnitvi, UC-7) nov dogodek ustvari novo opravilo.
- Odločitve ni mogoče spremeniti, ko je opravilo `Completed` (409).
- Hkratni popravki istega opravila: optimistično zaklepanje (`version`), poraženec dobi 409.

---

## 4. Dogodki (Kafka, CloudEvents 1.0, JSON)

Ime teme = tip dogodka. Ključ sporočila = `claimId` (vsi dogodki zahtevka so urejeni znotraj ene particije).

### Vhod: `review.requested.v1` (objavi ClaimsService)

Sprejme se CloudEvents ovojnica (`specversion`, `id`, `data`) **ali** samo objekt `data`. Imena polj niso občutljiva na velike/male črke, identifikatorji so lahko nizi ali števila.

| Polje v `data` | Obvezno | Opis |
|---|---|---|
| `claimId` | **da** | Id zahtevka (poljuben niz do 100 znakov) |
| `policyId`, `customerId` | ne | Id police / stranke |
| `claimedAmount` | ne | Zahtevani znesek (>= 0), alias `amount` |
| `currency` | ne | 3-črkovna koda, privzeto `EUR` |
| `riskScore` | ne | Ocena tveganja iz RiskService |
| `riskReasons` | ne | Seznam razlogov za ročni pregled |
| `recommendation` | ne | Predlog agenta (npr. `Approve`, `Reject`) |
| `summary` | ne | Povzetek agenta |

Primer: `samples/review-requested.json`.

Sporočilo, ki ga ni mogoče prebrati (neveljaven JSON, manjka `claimId`, ...), se prepiše v temo `review.requested.v1.dlq` z glavami `x-error`, `x-source-*`, da ne blokira particije. Druge napake (npr. baza ni dosegljiva) se ponavljajo z naraščajočim zamikom, odmik (offset) se potrdi šele po uspešnem zapisu.

### Izhod: `review.completed.v1`

```json
{
  "specversion": "1.0",
  "id": "5b0c8f7e-3a53-4b53-9d1e-7a6a2d4f9c10",
  "source": "/horica/review-service",
  "type": "review.completed.v1",
  "subject": "claims/CLM-2001",
  "time": "2026-11-07T10:12:31.5120000+00:00",
  "datacontenttype": "application/json",
  "data": {
    "reviewId": "0f6f1d4a-6a9e-4b0f-8f3e-1f0f2f2f9a11",
    "claimId": "CLM-2001",
    "decision": "Approved",
    "reason": "Documents are complete and consistent.",
    "reviewerId": "a3f1c2d4-0000-0000-0000-000000000001",
    "reviewerName": "ana",
    "decidedAt": "2026-11-07T10:12:31.4980000+00:00"
  }
}
```

| `decision` | Pomen za ClaimsService |
|---|---|
| `Approved` | zahtevek odobren |
| `Rejected` | zahtevek zavrnjen, `reason` je razlog |
| `NeedsMoreInfo` | stranka mora dopolniti dokazila (UC-4/UC-7), `reason` pove, kaj manjka |

Zanesljivost: odločitev in dogodek se zapišeta **v isti transakciji** (tabela `outbox_messages`), od tam ju ločen proces objavi v Kafko. Dostava je *vsaj enkrat*; `id` dogodka je stabilen, zato naj ga prejemniki uporabijo za deduplikacijo. Vhodni dogodki se deduplicirajo prek tabele `processed_events`.

---

## 5. Avtentikacija in vloge

| Okolje | Nastavitev | Delovanje |
|---|---|---|
| `Development` (privzeto pri `dotnet run` in v compose) | `Auth__Mode=Disabled` | Vsak klic je zaposleni `dev-reviewer` z vlogo `reviewer`. Za drugega uporabnika dodaj glavi `X-Dev-User: ana` in `X-Dev-Roles: reviewer,admin`. |
| Vsa ostala | `Auth__Mode=Jwt` (privzeto) | Preverja JWT iz Keycloaka. **Obvezno** nastavi `Auth__Authority`. |

Če `Auth__Mode=Disabled` poskusiš uporabiti izven `Development`, se storitev nalašč ne zažene.

Vloge (Keycloak *realm roles*, po želji tudi *client roles* za `Auth__ClientId`):

- `reviewer` – dela z opravili,
- `admin` – dodatno lahko prevzame ali sprosti opravila drugih.

Primer za Keycloak:

```
Auth__Authority=https://keycloak.example.com/realms/horica
Auth__Audience=              # prazno = audience se ne preverja (Keycloak privzeto da "account")
Auth__RequireHttpsMetadata=true
```

Če storitev doseže Keycloak na drugem naslovu, kot je zapisan v tokenu (tipično v Dockerju), nastavi še `Auth__MetadataAddress` (npr. `http://keycloak:8080/realms/horica/.well-known/openid-configuration`) in `Auth__ValidIssuer` (naslov, ki je v `iss` tokena).

---

## 6. Konfiguracija

Vse vrednosti so v `appsettings.json`; v okolju jih preglasiš s spremenljivkami (`:` postane `__`).

| Spremenljivka | Privzeto | Opis |
|---|---|---|
| `ConnectionStrings__ReviewDb` | `Host=localhost;Port=5432;Database=review_db;...` | Povezava na PostgreSQL |
| `Database__AutoCreate` | `true` | Ob zagonu ustvari tabele |
| `Kafka__Enabled` | `true` | `false` = brez Kafke (dogodki ostanejo v outboxu) |
| `Kafka__BootstrapServers` | `localhost:9092` | Naslov brokerja (v AKS npr. Strimzi bootstrap servis) |
| `Kafka__ConsumerGroupId` | `review-service` | Skupina potrošnikov |
| `Kafka__ReviewRequestedTopic` / `Kafka__ReviewCompletedTopic` | `review.requested.v1` / `review.completed.v1` | Imena tem |
| `Kafka__EnsureTopics`, `Kafka__TopicPartitions`, `Kafka__TopicReplicationFactor` | `true`, `3`, `1` | Samodejno ustvarjanje tem |
| `Kafka__SecurityProtocol`, `Kafka__SaslMechanism`, `Kafka__SaslUsername`, `Kafka__SaslPassword` | prazno | Za zavarovan Kafka (`SaslSsl`, `ScramSha512`, ...) |
| `Events__Source` | `/horica/review-service` | CloudEvents `source` |
| `Auth__*` | glej zgoraj | Avtentikacija |
| `Swagger__Enabled` | `false` (v Development `true`) | Swagger UI |

---

## 7. Baza podatkov

Lastna logična baza `review_db` (PostgreSQL):

- `review_tasks` – opravila (en odprt zahtevek = ena odprta vrstica, delni unikatni indeks),
- `review_history` – zgodovina sprememb,
- `outbox_messages` – dogodki, ki čakajo na objavo (po objavi dobijo `processed_at`),
- `processed_events` – že obdelani vhodni dogodki.

---

## 8. Vključitev v ostalo platformo

**YARP API Gateway** (primer poti `/api/reviews/...` → storitev `/reviews/...`):

```json
"ReverseProxy": {
  "Routes": {
    "reviews": {
      "ClusterId": "review-service",
      "Match": { "Path": "/api/reviews/{**catch-all}" },
      "Transforms": [ { "PathRemovePrefix": "/api" } ]
    }
  },
  "Clusters": {
    "review-service": {
      "Destinations": { "d1": { "Address": "http://review-service:8080/" } }
    }
  }
}
```

**AKS**: slika posluša na vratih `8080`; `livenessProbe` = `/health/live`, `readinessProbe` = `/health/ready`. Storitev je brez stanja v pomnilniku, lahko teče v več replikah (outbox lahko ob sočasnem delu več replik objavi isti dogodek dvakrat – zato dedupliciraj po `id`).

**Docker Hub / GitHub Actions**: `src/ReviewService/Dockerfile` ima kot kontekst mapo `src/ReviewService`.

---

## 9. Testi

```bash
dotnet test
```

Unit testi (ne potrebujejo Dockerja ali baze) pokrivajo pravila opravila (prevzem, sprostitev, odločitev, konflikti), branje vhodnega dogodka, obliko `review.completed.v1` in preslikavo Keycloak vlog.

---

## 10. Znane omejitve in možne razširitve

- Shema se ustvari z `EnsureCreated`. Ko se bo shema začela spreminjati, preklopi na EF Core migracije (`Microsoft.EntityFrameworkCore.Design`, `dotnet ef migrations add Initial`) in nastavi `Database__AutoCreate=false`.
- Stari zapisi v `outbox_messages` in `processed_events` se ne brišejo samodejno (po potrebi dodaj periodično čiščenje).
- Zaposleni v opravilu vidi podatke, ki jih prinese `review.requested.v1`. Če ClaimsService povzetka/predloga agenta ne vključi v dogodek, ga mora UI pridobiti iz drugih storitev.
- Ni vgrajenih metrik (OpenTelemetry/Prometheus); dodaj jih po potrebi.
