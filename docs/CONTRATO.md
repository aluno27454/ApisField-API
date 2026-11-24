# Contrato da API

Este é o contrato entre a API e a app Android. Qualquer alteração às rotas ou aos modelos atualiza este documento no mesmo pull request.

## Convenções

- Todas as rotas começam por `/api`.
- JSON em **camelCase**. Datas em ISO 8601, em **UTC** (`2026-11-13T10:57:00Z`). IDs inteiros.
- Autenticação: header `Authorization: Bearer <token>` em todas as rotas, exceto `GET /api/health`, `POST /api/auth/register` e `POST /api/auth/login`. Os tokens são válidos durante 30 dias.
- Mensagens de erro e textos gerados (alertas) em português de Portugal (AO90).

### Códigos de estado

| Código | Significado |
|---|---|
| 200 / 201 / 204 | Sucesso |
| 400 | Validação falhou: `ValidationProblemDetails`, com os erros por campo em `errors` |
| 401 | Sem token, token inválido ou expirado |
| 403 | Autenticado, mas sem permissão (ex.: editar o avistamento de outro utilizador) |
| 404 | Não existe, **ou é de outro utilizador**. Colmeias e visitas alheias devolvem 404, para não revelar que existem |
| 409 | Conflito (email já registado) |

## Modelos

```
User         { id, name, email, role: "user" | "admin" }
AuthResult   { token, user: User }

HiveLocation { latitude, longitude, label }
HiveSensors  { tempInterior, tempExterior, humidity, weight, co2 (null no modelo starter),
               battery, signal, waspDetected }
Hive         { id, name, model: "starter" | "pro",
               status: "healthy" | "warning" | "danger" | "offline",
               location: HiveLocation, sensors: HiveSensors, lastUpdate, ownerId }

SensorReading { label ("Seg".."Dom"), date ("2026-11-13"), value }   // value 0 = sem dados
HiveHistory   { temperature: SensorReading[7], weight: SensorReading[7],
                co2: SensorReading[] }                              // co2 vazio no modelo starter

Alert  { id, hiveId, hiveName, type: "wasp" | "temperature" | "weight" | "battery" | "signal" | "co2",
         severity: "warning" | "danger", message, timestamp, read }

Visit  { id, hiveId, hiveName, date, queenSeen, broodLevel: "fraca" | "media" | "forte",
         temperament: "calma" | "nervosa" | "agressiva", treatment?, notes?,
         latitude?, longitude?, photoIds: int[], authorId }

WaspSighting { id, type: "wasp" | "nest", latitude, longitude, accuracy?, notes?, date,
               photoId?, authorId, authorName, distanceKm? }   // distanceKm só quando se envia lat/lng
```

## Estado e alertas

O estado de uma colmeia é calculado a partir das leituras:

| Estado | Condição |
|---|---|
| `danger` | Vespa detetada **ou** temperatura interior > 37 °C |
| `warning` | CO₂ > 3500 ppm (só no modelo Pro), bateria < 20 % **ou** sinal < 30 % |
| `offline` | Sem leituras há mais de 2 horas |
| `healthy` | Nenhuma das anteriores |

Cada limiar ultrapassado gera um `Alert`, sem duplicar enquanto a condição se mantiver.

As colmeias criadas na app não têm hardware real. As suas leituras são simuladas por uma função determinística: o id da colmeia serve de semente e os valores variam com a hora do dia.

## Rotas

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/api/health` | — | 200 `{ "status": "ok" }` |
| POST | `/api/auth/register` | `{ name, email, password }` | 201 `AuthResult` · 409 |
| POST | `/api/auth/login` | `{ email, password }` | 200 `AuthResult` · 401 |
| GET | `/api/me` | — | `User` |
| GET | `/api/hives` | — | `Hive[]` do utilizador (o admin vê todas) |
| GET | `/api/hives/{id}` | — | `Hive` |
| GET | `/api/hives/{id}/history` | — | `HiveHistory` |
| POST | `/api/hives` | `{ name, model, latitude, longitude, label }` | 201 `Hive` |
| PUT | `/api/hives/{id}` | igual ao POST | `Hive` |
| DELETE | `/api/hives/{id}` | — | 204; apaga também as visitas, as fotos e os alertas |
| GET | `/api/alerts` | — | `Alert[]`, do mais recente para o mais antigo |
| PATCH | `/api/alerts/{id}/read` | — | `Alert` |
| PATCH | `/api/alerts/read-all` | — | 204 |
| GET | `/api/visits?hiveId=` | — | `Visit[]`, do mais recente para o mais antigo; `hiveId` é opcional (404 se a colmeia for de outro utilizador) |
| GET | `/api/visits/{id}` | — | `Visit` |
| POST | `/api/hives/{hiveId}/visits` | `{ date, queenSeen, broodLevel, temperament, treatment, notes, latitude, longitude }` | 201 `Visit` |
| PUT | `/api/visits/{id}` | igual ao POST | `Visit` |
| DELETE | `/api/visits/{id}` | — | 204 |
| POST | `/api/visits/{id}/photos` | corpo binário `image/jpeg` | 201 `{ id }` |
| GET | `/api/photos/{id}` | — | `image/jpeg` |
| DELETE | `/api/photos/{id}` | — | 204 |
| GET | `/api/sightings?lat=&lng=&radiusKm=` | — | `WaspSighting[]`: com `lat`/`lng`, os que estão dentro do raio (10 km por omissão), do mais perto para o mais longe; sem eles, todos, do mais recente para o mais antigo |
| GET | `/api/sightings/{id}` | — | `WaspSighting` |
| POST | `/api/sightings` | `{ type, latitude, longitude, accuracy, notes, date }` | 201 `WaspSighting` |
| PUT | `/api/sightings/{id}` | igual ao POST | `WaspSighting` (só o autor ou o admin) |
| DELETE | `/api/sightings/{id}` | — | 204 (só o autor ou o admin) |
| POST | `/api/sightings/{id}/photo` | `image/jpeg` | 201 `{ id }`; substitui a foto anterior |

## Validação (400)

| Campo | Regra |
|---|---|
| `name` | 2–60 caracteres |
| `email` | Formato válido, até 120 caracteres |
| `password` | Pelo menos 8 caracteres, com pelo menos uma letra e um dígito |
| `model` | `starter` ou `pro` |
| `latitude` / `longitude` | Entre −90 e 90 / entre −180 e 180 |
| `label` | Até 100 caracteres |
| `date` (visita) | No máximo 5 minutos no futuro |
| `broodLevel` / `temperament` | Valores do contrato |
| `treatment` | Até 200 caracteres |
| `notes` | Até 500 caracteres |
| Fotos | JPEG, até 2 MB; no máximo 5 por visita. O erro vem no campo `photo` |
