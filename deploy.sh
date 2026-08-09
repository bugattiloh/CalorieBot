#!/usr/bin/env bash
# Разворачивает CalorieBot одной командой: ./deploy.sh
# Собирает образ, поднимает postgres + bot через docker compose и ждёт, пока оба станут healthy.
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker не найден. Установите Docker (и Compose plugin) перед запуском: https://docs.docker.com/engine/install/" >&2
    exit 1
fi

if docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE=(docker-compose)
else
    echo "Не найден ни 'docker compose', ни 'docker-compose'." >&2
    exit 1
fi

if [ ! -f .env ]; then
    cp .env.example .env
    echo "Создал .env из .env.example."
    echo "Заполните в нём BOT_TOKEN (из @BotFather) и POSTGRES_PASSWORD, затем запустите ./deploy.sh ещё раз."
    exit 1
fi

# Секреты обязаны быть заполнены реальными значениями — placeholder'ы из .env.example не считаются.
missing=()
if ! grep -qE '^BOT_TOKEN=[0-9]+:.+' .env; then
    missing+=("BOT_TOKEN")
fi
if grep -qE '^POSTGRES_PASSWORD=\s*(change_me_please)?\s*$' .env; then
    missing+=("POSTGRES_PASSWORD")
fi
if [ "${#missing[@]}" -gt 0 ]; then
    echo "В .env не заполнены: ${missing[*]}. Отредактируйте .env и запустите скрипт снова." >&2
    exit 1
fi

echo "Собираю образ и поднимаю сервисы..."
"${COMPOSE[@]}" up -d --build

echo "Жду, пока postgres и bot станут healthy (до 2 минут)..."
deadline=$((SECONDS + 120))
while [ "$SECONDS" -lt "$deadline" ]; do
    statuses=$("${COMPOSE[@]}" ps --format '{{.Service}}: {{.Health}}' 2>/dev/null || true)

    if echo "$statuses" | grep -q "unhealthy"; then
        echo "Один из сервисов unhealthy:" >&2
        echo "$statuses" >&2
        echo "Логи: ${COMPOSE[*]} logs" >&2
        exit 1
    fi

    if [ -n "$statuses" ] && ! echo "$statuses" | grep -qE "starting|^$"; then
        echo "$statuses"
        break
    fi

    sleep 3
done

port="$(grep -E '^BOT_HTTP_PORT=' .env | cut -d= -f2)"
port="${port:-8080}"

echo
echo "Готово. Проверить: curl http://localhost:${port}/health"
echo "Логи бота: ${COMPOSE[*]} logs -f bot"
