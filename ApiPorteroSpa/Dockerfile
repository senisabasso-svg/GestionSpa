FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORTERO_TCP_PORT=8081 \
    PORTERO_DATA_DIR=/data

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

RUN mkdir -p /data /data/logs /data/logs/raw /data/logs/sessions

EXPOSE 8080 8081

CMD ["python", "run_all.py"]
