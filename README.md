# postgresql-cloud-dump
Only Google Cloud Buckets supported right now.

# How to Use This Image
```
docker run -d \
    -e PGDATABASE="YOUR_DATABASE_NAME" \
    -e PGHOST="localhost" \
    -e PGPORT="5432" \
    -e PGUSER="postgres" \
    -e PGPASSWORD="YOUR_STRONG_PASSWORD" \
    -e BACKUP_THRESHOLD="14d"
    -e BUCKET="NAME_OF_BUCKET_IN_GOOGLE_STORAGE" \
    -e GOOGLE_APPLICATION_CREDENTIALS="bucket_access.json"
    -v PATH_TO_SERVICE_ACCOUNT_JSON_FILE_ON_HOST:/app/bucket_access.json:ro
    settler/postgresql-cloud-dump
```

# How to Use This Image in Google Kubernetes Engine (GKE)
`kubectl create secret generic YOUR_SECRET_NAME --from-literal=password=YOUR_STRONG_PASSWORD`

`kubectl apply -f postgresql-cloud-dump.yaml`

**postgresql-cloud-dump.yaml:**
```
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: postgresql-cloud-dump
  namespace: default
spec:
  schedule: "0 */6 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: postgresql-cloud-dump
            image: settler/postgresql-cloud-dump
            env:
            - name: PGDATABASE
              value: YOUR_DATABASE_NAME
            - name: PGHOST
              value: POSTGRESQL_SERVICE_NAME.NAMESPACE
            - name: PGPORT
              value: "5432"
            - name: PGUSER
              value: postgres
            - name: PGPASSWORD
              valueFrom:
                secretKeyRef:
                  name: YOUR_SECRET_NAME
                  key: password
            - name: OUTPUT
              value: GoogleCloud
            - name: BUCKET
              value: NAME_OF_BUCKET_IN_GOOGLE_STORAGE
            - name: BACKUP_THRESHOLD
              value: 14d 
          restartPolicy: OnFailure
```
P.S. Do not need to provide GOOGLE_APPLICATION_CREDENTIALS for CronJob because it will run in Google Kubernetes Engine. If not, bind volume as described in docker example.
