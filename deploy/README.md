# Production deployment

Every push to `main` runs `.github/workflows/deploy.yml`:

1. Builds `ghcr.io/klitztuch/lanboard-web` (SDK container) and `ghcr.io/klitztuch/lanboard-migrate` (EF migration bundle), both tagged `latest` and `sha-<short>`.
2. Copies `docker-compose.yml` to the server and runs `docker compose pull && docker compose up -d --wait` with `LANBOARD_TAG=sha-<short>`.

On start, `migrate` applies pending migrations and exits; `web` only starts after it succeeded. The job fails if either doesn't come up.

## Server prerequisites

- Docker with Compose v2
- Traefik already running, with an external Docker network, an HTTPS entrypoint and a cert resolver
- A deploy user that can run `docker` and reach it over SSH

## First-time setup

On the server, as the deploy user:

```sh
mkdir ~/lanboard && cd ~/lanboard
# copy .env.example from the repo to .env and fill it in
chmod 600 .env
```

In GitHub (`Settings → Secrets and variables → Actions`):

| Name | Kind | Value |
|---|---|---|
| `DEPLOY_HOST` | secret | server host name or IP |
| `DEPLOY_USER` | secret | deploy user |
| `DEPLOY_SSH_KEY` | secret | private key of the deploy user (without passphrase) |
| `DEPLOY_KNOWN_HOSTS` | secret | output of `ssh-keyscan <host>` |
| `DEPLOY_PATH` | variable (optional) | directory with `.env`, relative to the deploy user's home; default `lanboard` |

The GHCR packages are private after the first push. The deploy job logs in with its own token, which expires when the job ends. For manual pulls (rollback), either make both packages public under `Packages → Package settings`, or run `docker login ghcr.io` on the server with a PAT that has `read:packages`.

The Steam login callback is `https://<LANBOARD_HOST>/signin-steam`; no registration with Steam needed, only the API key.

Then trigger the first deploy, either with a release merge into `main` or with `Run workflow` on the Deploy workflow.

### Bootstrapping the first admin

Log in once with Steam, then on the server:

```sh
docker compose exec postgres psql -U lanboard -d lanboard \
  -c "SELECT \"Id\", \"DisplayName\" FROM \"Users\";"
docker compose exec postgres psql -U lanboard -d lanboard \
  -c "INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\", \"AssignedAt\") VALUES ('<user-id>', '00000000-0000-0000-0000-00000000a0d1', now());"
```

Log out and in again so the cookie picks up the role.

## Rollback

```sh
cd ~/lanboard
LANBOARD_TAG=sha-<old> docker compose up -d --wait
```

This does not roll back migrations. The next deploy from `main` replaces the pinned tag again.

## Logs

```sh
docker compose logs -f web
docker compose logs migrate
```
