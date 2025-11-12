# Clients
[Android](https://github.com/Artifait/Starter_AndroidClient) <br>
[Pc](https://github.com/Artifait/Starter_PClient)

# Server container
[DockerHub](https://hub.docker.com/repository/docker/artifait/starter/general)


How run:
```bash
  docker run -d \
    --name starter-app \
    --restart=always \
    -p 8080:8080 \
    -v "$DATA_DIR":/app/data \
    -e DOTNET_ENVIRONMENT=Production \
    -e DATA_SOURCE="Data Source=/app/data/starter.db" \
    -e JWT_SECRET="${{ secrets.APP_JWT_SECRET }}" \
    -e HMAC_SECRET="${{ secrets.APP_HMAC_SECRET }}" \
    artifait/starter:latest
```
