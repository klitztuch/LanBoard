# Runs the self-contained EF migration bundle (built by the deploy workflow as deploy/efbundle) once and exits.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
COPY efbundle .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__postgres\""]
