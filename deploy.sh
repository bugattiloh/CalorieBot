#!/usr/bin/env bash
# Разворачивает CalorieBot на сервере одной командой: ./deploy.sh
#
# Никаких файлов редактировать вручную не нужно: скрипт сам ставит Docker, если его нет
# (с подтверждением в консоли), спрашивает токен бота при первом запуске и генерирует
# пароль базы данных. Дальше — собирает образ, поднимает postgres + bot и ждёт, пока оба
# станут healthy.
set -euo pipefail
cd "$(dirname "$0")"

confirm() {
    # confirm "Вопрос" — по умолчанию "да", если просто нажать Enter.
    local reply
    read -r -p "$1 [Y/n] " reply </dev/tty
    [[ -z "$reply" || "$reply" =~ ^[Yy] ]]
}

token_is_valid() {
    # Проверяю не только формат (цифры:буквы), но и что Telegram реально его принимает —
    # иначе легко случайно вставить старый/отозванный токен, и он молча пройдёт проверку.
    local token="$1"
    [[ "$token" =~ ^[0-9]+:.+ ]] || return 1
    curl -fsS "https://api.telegram.org/bot${token}/getMe" 2>/dev/null | grep -q '"ok":true'
}

# --- 1. Docker и Compose ---------------------------------------------------

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker на этом сервере не найден."
    if ! confirm "Установить Docker автоматически (официальный скрипт get.docker.com)?"; then
        echo "Без Docker продолжить не могу. Установите его вручную: https://docs.docker.com/engine/install/" >&2
        exit 1
    fi

    INSTALL_SUDO=""
    [ "$(id -u)" -ne 0 ] && INSTALL_SUDO="sudo"

    curl -fsSL https://get.docker.com | $INSTALL_SUDO sh
    $INSTALL_SUDO systemctl enable --now docker >/dev/null 2>&1 || true
    echo "Docker установлен."
fi

# Докеру после свежей установки иногда нужны root-права, пока пользователя не перелогинили
# в группу docker — подстраховываюсь через sudo, если без него команды не проходят.
DOCKER_SUDO=""
if [ "$(id -u)" -ne 0 ] && ! docker info >/dev/null 2>&1; then
    DOCKER_SUDO="sudo"
fi

if $DOCKER_SUDO docker compose version >/dev/null 2>&1; then
    COMPOSE=($DOCKER_SUDO docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE=($DOCKER_SUDO docker-compose)
else
    echo "Docker поставился, но Compose plugin не нашёлся — обновите Docker: https://docs.docker.com/compose/install/" >&2
    exit 1
fi

# --- 2. .env: токен бота и пароль базы --------------------------------------

if [ ! -f .env ]; then
    cp .env.example .env
    chmod 600 .env
fi

current_token="$(grep -E '^BOT_TOKEN=' .env | cut -d= -f2-)"
if ! token_is_valid "$current_token"; then
    echo
    echo "Нужен действующий токен бота из @BotFather (там: /mybots → выбрать бота → API Token)."
    while true; do
        read -r -p "Вставьте BOT_TOKEN: " bot_token </dev/tty
        if token_is_valid "$bot_token"; then
            break
        fi
        echo "Telegram не признал этот токен — либо неверный формат, либо он отозван/устарел. Проверьте в @BotFather и попробуйте ещё раз."
    done
    # Экранирую спецсимволы sed, чтобы токен с произвольными символами не сломал замену.
    escaped_token=$(printf '%s' "$bot_token" | sed 's/[&/\]/\\&/g')
    sed -i "s|^BOT_TOKEN=.*|BOT_TOKEN=${escaped_token}|" .env
    echo "Токен проверен и сохранён."
fi

current_password="$(grep -E '^POSTGRES_PASSWORD=' .env | cut -d= -f2-)"
if [ -z "$current_password" ] || [ "$current_password" = "change_me_please" ]; then
    generated_password="$(tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32)"
    sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=${generated_password}|" .env
    echo "Сгенерировал случайный пароль для PostgreSQL и сохранил его в .env на сервере."
fi

# --- 3. Сборка и запуск ------------------------------------------------------

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
echo "Открыть бота в Telegram и отправить /start."
