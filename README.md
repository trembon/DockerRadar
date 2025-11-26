# DockerRadar

Simple solution to monitor for Docker container image updates.

## Reason

I just wanted to have a simple way to monitor for updates to Docker container images that I use for my server.
I couldnt get any of the existing tools to work the way I wanted, with just checking the specified tag and not other tags, so I built this to show the number of running containers with updates on my dashboard.

## Features

- Reads containers from the Docker Engine API
- Checks for updates to the container images based on the current tag
- Supports DockerHub (docker.io), GitHub (ghcr.io), Microsoft (mcr.microsoft.com) and LinuxServer (lscr.io) container registries
- Basic JSON endpoints for integrations

## Example docker compose

```yaml
services:
  docker-radar:
    image: ghcr.io/trembon/dockerradar:main
    container_name: docker-radar
    restart: unless-stopped
    ports:
      - 24322:8080
    environment:
      - APP_Provider__ghcr.io__Enabled=<true/false>
      - APP_Provider__ghcr.io__Username=<username>
      - APP_Provider__ghcr.io__Token=<pat_token>
      - APP_Provider__docker.io__Enabled=<true/false>
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
```

## Endpoints

### List containers

**URL:** /

**Flags:**
| Name | Values | Default | Description |
|------|--------|---------|-------------|
| html | true/false | false | show the output as a simple HTML table |
| updates | true/false | false | only show containers with updates |
| running | true/false | true | only show running containers |

**Examples:** http://localhost:12324/?html=true&updates=true

**JSON Output:**

```json
[
  {
    "id": "<id>",
    "name": "docker-radar",
    "image": "ghcr.io/trembon/dockerradar:main",
    "digest": "sha256:<digest>",
    "remoteDigest": null,
    "status": "running",
    "hasUpdate": false,
    "updateCheckFailed": false,
    "lastChecked": "2025-11-26T09:39:19.3195538Z",
    "nextCheck": "2025-11-26T10:15:42.4424151Z"
  },
  {
    "id": "<id>",
    "name": "postgres",
    "image": "postgres:16-alpine",
    "digest": "sha256:<digest>",
    "remoteDigest": null,
    "status": "running",
    "hasUpdate": false,
    "updateCheckFailed": false,
    "lastChecked": "2025-11-26T09:24:19.1800581Z",
    "nextCheck": "2025-11-26T10:04:23.4546232Z"
  }
]
```

### get statistics

**URL:** /stats

**Examples:** http://localhost:12324/stats

```json
{
  "total": 63,
  "running": 55,
  "haveUpdate": 8,
  "runningAndHaveUpdate": 7
}
```
