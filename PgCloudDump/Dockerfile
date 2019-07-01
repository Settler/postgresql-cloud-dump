FROM microsoft/dotnet:sdk AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ./PgCloudDump.csproj ./
RUN dotnet restore PgCloudDump.csproj

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out --no-restore

# Build runtime image
FROM microsoft/dotnet:aspnetcore-runtime

RUN apt-get update\
  && apt-get install -y gnupg\
  && echo "deb http://apt.postgresql.org/pub/repos/apt/ stretch-pgdg main" > /etc/apt/sources.list.d/pgdg.list\
  && curl -L https://www.postgresql.org/media/keys/ACCC4CF8.asc | apt-key add -\
  && apt-get update\
  && apt-get install -y postgresql-client\
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build-env /app/out .

ENV PGDATABASE="your_database"
ENV PGHOST="localhost"
ENV PGPORT=5432
ENV PGUSER="postgres"
ENV PGPASSWORD="YOUR_STRONG_PASSWORD"
ENV OUTPUT="GoogleCloud"
ENV BUCKET="test-bucket"
ENV HOST_PATH="/app/backups"
ENV BACKUP_THRESHOLD="14d"

CMD ["sh", "-c",\
"dotnet PgCloudDump.dll \
-d $PGDATABASE \
-h $PGHOST \
-p $PGPORT \
-U $PGUSER \
-W $PGPASSWORD \
-o $OUTPUT \
-b $BUCKET \
-r $BACKUP_THRESHOLD \
--path $HOST_PATH \
--pg-dump-path pg_dump \
"]