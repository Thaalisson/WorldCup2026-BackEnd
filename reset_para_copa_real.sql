-- ============================================================
-- RESET do chaveamento fictício -> preparar para importar a Copa REAL
-- Rodar no Supabase SQL Editor UMA vez, ANTES de chamar /api/admin/import-fixtures
--
-- O que APAGA: jogos, seleções e tudo que depende deles (palpites, feed,
--              snapshots de ranking, jogadores).
-- O que MANTÉM: usuários, pools e participantes (ninguém precisa se recadastrar).
--
-- Depois deste reset, os participantes vão refazer os palpites sobre os
-- jogos REAIS importados da API.
-- ============================================================

BEGIN;

-- 1) Filhos que dependem de Matches
DELETE FROM "Predictions";
DELETE FROM "FeedEvents";

-- 2) Filhos que dependem de Teams
DELETE FROM "GroupPredictions";
DELETE FROM "ChampionPredictions";
DELETE FROM "Players";

-- 3) Histórico de pontuação (será recalculado conforme os jogos reais terminam)
DELETE FROM "RankingSnapshots";

-- 4) Por fim, o torneio em si
DELETE FROM "Matches";
DELETE FROM "Teams";

COMMIT;

-- Conferência (deve retornar 0 em ambos):
-- SELECT (SELECT COUNT(*) FROM "Matches") AS matches, (SELECT COUNT(*) FROM "Teams") AS teams;
