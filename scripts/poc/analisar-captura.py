#!/usr/bin/env python3
"""
analisar-captura.py — Extrai as métricas da prova de conceito a partir do log do
Worker (Etapa 1.3). Mede o NOSSO pipeline ponta a ponta; a caracterização da API
do CNJ em si é feita por djen-latency.sh / djen-ratelimit.sh.

Uso: scripts/poc/analisar-captura.py <arquivo.log>
"""
import re
import sys
from datetime import datetime

LOG = sys.argv[1]
ts = lambda linha: datetime.strptime(re.match(r'\[(\d{2}:\d{2}:\d{2})', linha).group(1), '%H:%M:%S')

paginas, capturas, concluidas, erros = [], [], [], []
for linha in open(LOG, encoding='utf-8', errors='replace'):
    if not re.match(r'\[\d{2}:\d{2}:\d{2}', linha):
        continue
    if 'comunicações recebidas' in linha:
        paginas.append(ts(linha))
    elif 'Publicação capturada' in linha:
        capturas.append(ts(linha))
    elif 'CnjCaptureJob concluído' in linha:
        concluidas.append(linha.strip())
    elif 'CNJ_RATE_LIMITED' in linha or 'limitador de taxa' in linha or '23505' in linha:
        erros.append(linha.strip()[:120])

print(f"paginas buscadas no DJEN : {len(paginas)}")
print(f"publicacoes persistidas  : {len(capturas)}")

if len(paginas) > 1:
    janela = (paginas[-1] - paginas[0]).total_seconds()
    print(f"janela de varredura      : {janela:.0f}s")
    if janela > 0:
        print(f"vazao de requisicoes     : {len(paginas)/janela:.2f} req/s  (teto do CNJ ~ 4,0 req/s)")
        print(f"vazao de publicacoes     : {len(capturas)/janela:.1f} pub/s")
    intervalos = [(b - a).total_seconds() for a, b in zip(paginas, paginas[1:])]
    if intervalos:
        print(f"intervalo medio/pagina   : {sum(intervalos)/len(intervalos):.2f}s")

print(f"eventos de rate limit    : {len(erros)}")
for e in erros[:3]:
    print(f"  {e}")
print()
for c in concluidas:
    print(c)
