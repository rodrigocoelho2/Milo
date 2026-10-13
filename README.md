# CorpoOps — servidor de sincronização

Transforma o CorpoOps de um ficheiro HTML local numa app sincronizada: registas
no telemóvel no ginásio, vês no PC em casa. Os dados vivem num ficheiro SQLite na
tua NAS, atrás do Nginx Proxy Manager, na `nas-net`.

## Como funciona

- **Um servidor Node minúsculo** (`server.js`, sem framework) serve a app e expõe
  dois endpoints: `GET /api/state` e `POST /api/state`.
- **Estado = um documento JSON.** A app carrega-o ao abrir e envia-o de volta a
  cada alteração (com debounce de ~1s). Não há esquema de base de dados a manter.
- **SQLite** guarda esse JSON num único ficheiro (`data/corpoops.db`). O backup és
  tu a copiar o ficheiro — entra no teu esquema de backups como tudo o resto.
- **Offline-first.** A app continua a usar o `localStorage` como cache. Se a NAS
  estiver em baixo, continuas a registar; sincroniza quando voltar a haver ligação.
- **Auth por token partilhado.** Defines um `CORPOOPS_TOKEN`; cada dispositivo
  guarda-o localmente. Sem token válido, os endpoints devolvem 401.

> Nota: é **single-user**. Há um único documento de estado partilhado. Não foi
> pensado para várias pessoas com dados separados — para o teu caso, é o que
> queres. A última escrita vence (não há merge de edições simultâneas em dois
> dispositivos ao mesmo tempo; na prática, registas num sítio de cada vez).

## Ficheiros

```
corpoops-server/
├─ server.js            # o servidor
├─ package.json
├─ Dockerfile
├─ docker-compose.yml   # já ligado à nas-net
├─ .env.example         # copia para .env
├─ public/
│  └─ index.html        # a app (versão com sync)
└─ data/                # criado no primeiro arranque; contém o SQLite
```

## Passos

### 1. Gera um token

```bash
openssl rand -hex 24
```

Copia `.env.example` para `.env` e cola lá o token:

```bash
cp .env.example .env
# edita .env e mete o CORPOOPS_TOKEN
```

### 2. Sobe o container

A `docker-compose.yml` já assume que a rede externa `nas-net` existe (a mesma que
usas para o NPM). Não publica porta no host — o NPM chega ao container pelo nome.

```bash
docker compose up -d --build
```

Confirma que está de pé:

```bash
docker compose logs -f corpoops
# deves ver: CorpoOps sync server listening on :8971
```

### 3. Aponta o Nginx Proxy Manager

No NPM, cria um **Proxy Host**:

- **Domain Names:** `corpoops.oteudominio.xyz` (ou um subdomínio à tua escolha)
- **Scheme:** `http`
- **Forward Hostname/IP:** `corpoops`  ← o nome do container, resolvido via `nas-net`
- **Forward Port:** `8971`
- **Websockets:** não é preciso
- **Block Common Exploits:** podes ligar
- No separador **SSL**, pede um certificado Let's Encrypt (ou usa o teu wildcard)

Como o container e o NPM partilham a `nas-net`, o forward por nome funciona sem
publicares portas.

### 4. (Opcional) Cloudflare

Se já expões serviços via Cloudflare Tunnel / DNS, aponta o subdomínio para o teu
NPM como fazes com o resto. A app não precisa de nada especial da Cloudflare; o
header real de IP não é relevante aqui porque a auth é por token.

Para acesso só dentro de casa, salta a Cloudflare e usa o domínio interno.

### 5. Abre e configura o token

1. Abre `https://corpoops.oteudominio.xyz` no PC.
2. Na barra lateral, em baixo, vê o estado de sincronização. Toca nele.
3. Cola o `CORPOOPS_TOKEN`. Guarda. Deve dizer **"ligado ✓"**.
4. No telemóvel, abre o mesmo endereço, toca no estado de sync (menu inferior →
   ou no cabeçalho), mete o mesmo token. A partir daí, os dois falam com a NAS.

**Dica:** no telemóvel, usa "Adicionar ao ecrã principal" no browser — abre em
ecrã inteiro, como uma app.

## Migrar os teus dados atuais

Se já usavas a versão local (ficheiro único) e tens dados:

1. Na versão antiga, **Exportar backup** → guardas o `.json`.
2. Na versão nova (já ligada ao servidor), **Importar backup** → escolhe esse
   ficheiro. Ele sobe para a NAS e passa a ser a fonte de verdade.

## Backups

O estado inteiro é `data/corpoops.db` (+ `-wal`/`-shm` temporários). Para um
backup consistente:

```bash
# cópia simples (com o container a correr é seguro em modo WAL para leitura)
cp data/corpoops.db /caminho/dos/backups/corpoops-$(date +%F).db

# ou, mais correto, usando o próprio sqlite:
docker compose exec corpoops node -e "const d=require('better-sqlite3')('/data/corpoops.db'); d.exec(\"VACUUM INTO '/data/backup.db'\"); console.log('ok')"
```

Aponta o teu esquema de backups habitual a esta pasta e está feito.

## Atualizar a app

Quando eu te der uma nova versão do `index.html`, substitui
`public/index.html` e:

```bash
docker compose up -d --build
```

Os dados não são afetados (estão no volume `data/`).

## Resolução de problemas

- **"token recusado pelo servidor"** → o token no browser não bate certo com o
  `CORPOOPS_TOKEN` do container. Confirma o `.env` e reinicia (`docker compose up -d`).
- **"sem ligação ao servidor"** → o NPM não está a encaminhar, ou o container não
  está de pé. Vê `docker compose logs corpoops` e o teste `GET /api/health`.
- **Testar o servidor diretamente** (sem NPM): descomenta o bloco `ports:` na
  compose, sobe, e faz:
  ```bash
  curl http://IP-DA-NAS:8971/api/health
  ```
- **better-sqlite3 não compila** (NAS ARM antiga) → o `Dockerfile` já inclui
  `python3/make/g++` para compilar se o binário pré-construído não servir.

## Segurança — o essencial

- O token é a única barreira: **usa um longo e aleatório** (o `openssl rand -hex 24`
  dá-te 48 hex chars). Serve-o sempre por HTTPS (o NPM trata disso).
- A comparação do token é feita em tempo constante, para não vazar por timing.
- O servidor corre como utilizador não-root dentro do container.
- Não há registo de dados pessoais nos logs; só arranque e erros.
