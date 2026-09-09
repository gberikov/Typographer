#!/usr/bin/env bash
# Разовый снимок веб-сервиса Лебедева: вход -> выход оракула.
# Запускать вручную, результат коммитится. Тесты сеть не трогают.
#
# entityType=3 — символы, а не сущности (проверено пробным запросом).
set -euo pipefail

ENDPOINT="https://typograf.artlebedev.ru/webservices/typograf.asmx"
ACTION="http://typograf.artlebedev.ru/webservices/ProcessText"
INPUTS="$(dirname "$0")/oracle-inputs.txt"
OUT="$(dirname "$0")/../docs/oracle/lebedev.md"

escape() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'; }

# На Windows curl.exe получает argv через MSYS bash, и кириллица в аргументе
# --data-binary "$body" уродуется перекодировкой argv (проверено: тот же
# запрос через файл проходит, тот же текст как аргумент — нет). Поэтому тело
# запроса пишем во временный файл и передаём через --data-binary @file.
REQ="$(mktemp)"
trap 'rm -f "$REQ"' EXIT

mkdir -p "$(dirname "$OUT")"
{
    echo "# Снимок веб-сервиса Лебедева"
    echo
    echo "Снят скриптом \`tools/oracle-snapshot.sh\` для планов 2b и 2c."
    echo "Настройки запроса: entityType=3 (символы), useBr=false, useP=false, maxNobr=0."
    echo "Неразрывный пробел показан как \`_\`, длинное тире как \`—\`."
    echo
    echo "| Вход | Выход оракула |"
    echo "|---|---|"
} > "$OUT"

while IFS= read -r line; do
    [ -z "$line" ] && continue
    body="<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><ProcessText xmlns=\"http://typograf.artlebedev.ru/webservices/\"><text>$(escape "$line")</text><entityType>3</entityType><useBr>false</useBr><useP>false</useP><maxNobr>0</maxNobr></ProcessText></soap:Body></soap:Envelope>"
    printf '%s' "$body" > "$REQ"

    response=$(curl -sS -m 30 -X POST \
        -H 'Content-Type: text/xml; charset=utf-8' \
        -H "SOAPAction: \"$ACTION\"" \
        --data-binary "@$REQ" "$ENDPOINT")

    result=$(printf '%s' "$response" \
        | tr '\n' $'\x01' \
        | sed -e 's/.*<ProcessTextResult[^>]*>//' -e 's|</ProcessTextResult>.*||' \
        | tr $'\x01' '\n' \
        | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' \
        | sed -e 's/\xc2\xa0/_/g' -e 's/|/\\|/g')

    printf '| %s | %s |\n' "$(printf '%s' "$line" | sed -e 's/|/\\|/g')" "$result" >> "$OUT"
    sleep 1   # сервис чужой: один запрос в секунду, не чаще
done < "$INPUTS"

echo "Снимок записан в $OUT"
