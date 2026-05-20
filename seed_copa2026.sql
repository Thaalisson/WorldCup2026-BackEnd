-- ============================================================
-- SEED Copa do Mundo 2026 — 49 seleções + 104 jogos
-- Rodar no Supabase SQL Editor (apenas uma vez)
-- ============================================================

DO $$
DECLARE
  -- Grupo A
  v_mexico        UUID := gen_random_uuid();
  v_africa_sul    UUID := gen_random_uuid();
  v_coreia_sul    UUID := gen_random_uuid();
  v_tchequia      UUID := gen_random_uuid();
  -- Grupo B
  v_canada        UUID := gen_random_uuid();
  v_bosnia        UUID := gen_random_uuid();
  v_catar         UUID := gen_random_uuid();
  v_suica         UUID := gen_random_uuid();
  -- Grupo C
  v_brasil        UUID := gen_random_uuid();
  v_marrocos      UUID := gen_random_uuid();
  v_haiti         UUID := gen_random_uuid();
  v_escocia       UUID := gen_random_uuid();
  -- Grupo D
  v_eua           UUID := gen_random_uuid();
  v_paraguai      UUID := gen_random_uuid();
  v_australia     UUID := gen_random_uuid();
  v_turquia       UUID := gen_random_uuid();
  -- Grupo E
  v_alemanha      UUID := gen_random_uuid();
  v_curacao       UUID := gen_random_uuid();
  v_costa_marfim  UUID := gen_random_uuid();
  v_equador       UUID := gen_random_uuid();
  -- Grupo F
  v_holanda       UUID := gen_random_uuid();
  v_japao         UUID := gen_random_uuid();
  v_suecia        UUID := gen_random_uuid();
  v_tunisia       UUID := gen_random_uuid();
  -- Grupo G
  v_belgica       UUID := gen_random_uuid();
  v_egito         UUID := gen_random_uuid();
  v_ira           UUID := gen_random_uuid();
  v_nova_zelandia UUID := gen_random_uuid();
  -- Grupo H
  v_espanha       UUID := gen_random_uuid();
  v_cabo_verde    UUID := gen_random_uuid();
  v_arabia        UUID := gen_random_uuid();
  v_uruguai       UUID := gen_random_uuid();
  -- Grupo I
  v_franca        UUID := gen_random_uuid();
  v_senegal       UUID := gen_random_uuid();
  v_iraque        UUID := gen_random_uuid();
  v_noruega       UUID := gen_random_uuid();
  -- Grupo J
  v_argentina     UUID := gen_random_uuid();
  v_argelia       UUID := gen_random_uuid();
  v_austria       UUID := gen_random_uuid();
  v_jordania      UUID := gen_random_uuid();
  -- Grupo K
  v_portugal      UUID := gen_random_uuid();
  v_rd_congo      UUID := gen_random_uuid();
  v_uzbequistao   UUID := gen_random_uuid();
  v_colombia      UUID := gen_random_uuid();
  -- Grupo L
  v_inglaterra    UUID := gen_random_uuid();
  v_croacia       UUID := gen_random_uuid();
  v_gana          UUID := gen_random_uuid();
  v_panama        UUID := gen_random_uuid();
  -- Mata-mata (A Definir)
  v_tbd           UUID := gen_random_uuid();

BEGIN

  -- ============================================================
  -- 49 SELEÇÕES
  -- ============================================================
  INSERT INTO "Teams" ("Id", "Name", "Code", "IsoCode", "GroupName") VALUES
    -- Grupo A
    (v_mexico,        'México',               'MEX', 'mx',     'A'),
    (v_africa_sul,    'África do Sul',        'RSA', 'za',     'A'),
    (v_coreia_sul,    'Coreia do Sul',        'KOR', 'kr',     'A'),
    (v_tchequia,      'Tchéquia',             'CZE', 'cz',     'A'),
    -- Grupo B
    (v_canada,        'Canadá',               'CAN', 'ca',     'B'),
    (v_bosnia,        'Bósnia e Herzegovina', 'BIH', 'ba',     'B'),
    (v_catar,         'Catar',                'QAT', 'qa',     'B'),
    (v_suica,         'Suíça',                'SUI', 'ch',     'B'),
    -- Grupo C
    (v_brasil,        'Brasil',               'BRA', 'br',     'C'),
    (v_marrocos,      'Marrocos',             'MAR', 'ma',     'C'),
    (v_haiti,         'Haiti',                'HAI', 'ht',     'C'),
    (v_escocia,       'Escócia',              'SCO', 'gb-sct', 'C'),
    -- Grupo D
    (v_eua,           'EUA',                  'USA', 'us',     'D'),
    (v_paraguai,      'Paraguai',             'PAR', 'py',     'D'),
    (v_australia,     'Austrália',            'AUS', 'au',     'D'),
    (v_turquia,       'Turquia',              'TUR', 'tr',     'D'),
    -- Grupo E
    (v_alemanha,      'Alemanha',             'GER', 'de',     'E'),
    (v_curacao,       'Curaçao',              'CUW', 'cw',     'E'),
    (v_costa_marfim,  'Costa do Marfim',      'CIV', 'ci',     'E'),
    (v_equador,       'Equador',              'ECU', 'ec',     'E'),
    -- Grupo F
    (v_holanda,       'Holanda',              'NED', 'nl',     'F'),
    (v_japao,         'Japão',                'JPN', 'jp',     'F'),
    (v_suecia,        'Suécia',               'SWE', 'se',     'F'),
    (v_tunisia,       'Tunísia',              'TUN', 'tn',     'F'),
    -- Grupo G
    (v_belgica,       'Bélgica',              'BEL', 'be',     'G'),
    (v_egito,         'Egito',                'EGY', 'eg',     'G'),
    (v_ira,           'Irã',                  'IRN', 'ir',     'G'),
    (v_nova_zelandia, 'Nova Zelândia',        'NZL', 'nz',     'G'),
    -- Grupo H
    (v_espanha,       'Espanha',              'ESP', 'es',     'H'),
    (v_cabo_verde,    'Cabo Verde',           'CPV', 'cv',     'H'),
    (v_arabia,        'Arábia Saudita',       'KSA', 'sa',     'H'),
    (v_uruguai,       'Uruguai',              'URU', 'uy',     'H'),
    -- Grupo I
    (v_franca,        'França',               'FRA', 'fr',     'I'),
    (v_senegal,       'Senegal',              'SEN', 'sn',     'I'),
    (v_iraque,        'Iraque',               'IRQ', 'iq',     'I'),
    (v_noruega,       'Noruega',              'NOR', 'no',     'I'),
    -- Grupo J
    (v_argentina,     'Argentina',            'ARG', 'ar',     'J'),
    (v_argelia,       'Argélia',              'ALG', 'dz',     'J'),
    (v_austria,       'Áustria',              'AUT', 'at',     'J'),
    (v_jordania,      'Jordânia',             'JOR', 'jo',     'J'),
    -- Grupo K
    (v_portugal,      'Portugal',             'POR', 'pt',     'K'),
    (v_rd_congo,      'RD Congo',             'COD', 'cd',     'K'),
    (v_uzbequistao,   'Uzbequistão',          'UZB', 'uz',     'K'),
    (v_colombia,      'Colômbia',             'COL', 'co',     'K'),
    -- Grupo L
    (v_inglaterra,    'Inglaterra',           'ENG', 'gb-eng', 'L'),
    (v_croacia,       'Croácia',              'CRO', 'hr',     'L'),
    (v_gana,          'Gana',                 'GHA', 'gh',     'L'),
    (v_panama,        'Panamá',               'PAN', 'pa',     'L'),
    -- Placeholder mata-mata
    (v_tbd,           'A Definir',            'TBD', NULL,     NULL);

  -- ============================================================
  -- 72 JOGOS — FASE DE GRUPOS
  -- Stage=1 (GroupStage)
  -- ============================================================
  INSERT INTO "Matches" ("Id", "FifaMatchCode", "HomeTeamId", "AwayTeamId", "KickoffAt", "Stage", "GroupName", "IsFinished") VALUES
    -- GRUPO A
    (gen_random_uuid(), '1',  v_mexico,       v_africa_sul,    '2026-06-11 19:00:00', 1, 'A', FALSE),
    (gen_random_uuid(), '2',  v_coreia_sul,   v_tchequia,      '2026-06-12 02:00:00', 1, 'A', FALSE),
    (gen_random_uuid(), '25', v_tchequia,     v_africa_sul,    '2026-06-18 16:00:00', 1, 'A', FALSE),
    (gen_random_uuid(), '28', v_mexico,       v_coreia_sul,    '2026-06-19 01:00:00', 1, 'A', FALSE),
    (gen_random_uuid(), '53', v_tchequia,     v_mexico,        '2026-06-25 01:00:00', 1, 'A', FALSE),
    (gen_random_uuid(), '54', v_africa_sul,   v_coreia_sul,    '2026-06-25 01:00:00', 1, 'A', FALSE),
    -- GRUPO B
    (gen_random_uuid(), '3',  v_canada,       v_bosnia,        '2026-06-12 19:00:00', 1, 'B', FALSE),
    (gen_random_uuid(), '5',  v_catar,        v_suica,         '2026-06-13 19:00:00', 1, 'B', FALSE),
    (gen_random_uuid(), '26', v_suica,        v_bosnia,        '2026-06-18 19:00:00', 1, 'B', FALSE),
    (gen_random_uuid(), '27', v_canada,       v_catar,         '2026-06-18 22:00:00', 1, 'B', FALSE),
    (gen_random_uuid(), '49', v_suica,        v_canada,        '2026-06-24 19:00:00', 1, 'B', FALSE),
    (gen_random_uuid(), '50', v_bosnia,       v_catar,         '2026-06-24 19:00:00', 1, 'B', FALSE),
    -- GRUPO C
    (gen_random_uuid(), '6',  v_brasil,       v_marrocos,      '2026-06-13 22:00:00', 1, 'C', FALSE),
    (gen_random_uuid(), '7',  v_haiti,        v_escocia,       '2026-06-14 01:00:00', 1, 'C', FALSE),
    (gen_random_uuid(), '30', v_escocia,      v_marrocos,      '2026-06-19 22:00:00', 1, 'C', FALSE),
    (gen_random_uuid(), '31', v_brasil,       v_haiti,         '2026-06-20 00:30:00', 1, 'C', FALSE),
    (gen_random_uuid(), '51', v_escocia,      v_brasil,        '2026-06-24 22:00:00', 1, 'C', FALSE),
    (gen_random_uuid(), '52', v_marrocos,     v_haiti,         '2026-06-24 22:00:00', 1, 'C', FALSE),
    -- GRUPO D
    (gen_random_uuid(), '4',  v_eua,          v_paraguai,      '2026-06-13 01:00:00', 1, 'D', FALSE),
    (gen_random_uuid(), '8',  v_australia,    v_turquia,       '2026-06-14 04:00:00', 1, 'D', FALSE),
    (gen_random_uuid(), '29', v_eua,          v_australia,     '2026-06-19 19:00:00', 1, 'D', FALSE),
    (gen_random_uuid(), '32', v_turquia,      v_paraguai,      '2026-06-20 03:00:00', 1, 'D', FALSE),
    (gen_random_uuid(), '59', v_turquia,      v_eua,           '2026-06-26 02:00:00', 1, 'D', FALSE),
    (gen_random_uuid(), '60', v_paraguai,     v_australia,     '2026-06-26 02:00:00', 1, 'D', FALSE),
    -- GRUPO E
    (gen_random_uuid(), '9',  v_alemanha,     v_curacao,       '2026-06-14 17:00:00', 1, 'E', FALSE),
    (gen_random_uuid(), '11', v_costa_marfim, v_equador,       '2026-06-14 23:00:00', 1, 'E', FALSE),
    (gen_random_uuid(), '34', v_alemanha,     v_costa_marfim,  '2026-06-20 20:00:00', 1, 'E', FALSE),
    (gen_random_uuid(), '35', v_equador,      v_curacao,       '2026-06-21 00:00:00', 1, 'E', FALSE),
    (gen_random_uuid(), '55', v_curacao,      v_costa_marfim,  '2026-06-25 20:00:00', 1, 'E', FALSE),
    (gen_random_uuid(), '56', v_equador,      v_alemanha,      '2026-06-25 20:00:00', 1, 'E', FALSE),
    -- GRUPO F
    (gen_random_uuid(), '10', v_holanda,      v_japao,         '2026-06-14 20:00:00', 1, 'F', FALSE),
    (gen_random_uuid(), '12', v_suecia,       v_tunisia,       '2026-06-15 02:00:00', 1, 'F', FALSE),
    (gen_random_uuid(), '33', v_holanda,      v_suecia,        '2026-06-20 17:00:00', 1, 'F', FALSE),
    (gen_random_uuid(), '36', v_tunisia,      v_japao,         '2026-06-21 04:00:00', 1, 'F', FALSE),
    (gen_random_uuid(), '57', v_japao,        v_suecia,        '2026-06-25 23:00:00', 1, 'F', FALSE),
    (gen_random_uuid(), '58', v_tunisia,      v_holanda,       '2026-06-25 23:00:00', 1, 'F', FALSE),
    -- GRUPO G
    (gen_random_uuid(), '14', v_belgica,      v_egito,         '2026-06-15 19:00:00', 1, 'G', FALSE),
    (gen_random_uuid(), '16', v_ira,          v_nova_zelandia, '2026-06-16 01:00:00', 1, 'G', FALSE),
    (gen_random_uuid(), '38', v_belgica,      v_ira,           '2026-06-21 19:00:00', 1, 'G', FALSE),
    (gen_random_uuid(), '40', v_nova_zelandia,v_egito,         '2026-06-22 01:00:00', 1, 'G', FALSE),
    (gen_random_uuid(), '65', v_egito,        v_ira,           '2026-06-27 03:00:00', 1, 'G', FALSE),
    (gen_random_uuid(), '66', v_nova_zelandia,v_belgica,       '2026-06-27 03:00:00', 1, 'G', FALSE),
    -- GRUPO H
    (gen_random_uuid(), '13', v_espanha,      v_cabo_verde,    '2026-06-15 16:00:00', 1, 'H', FALSE),
    (gen_random_uuid(), '15', v_arabia,       v_uruguai,       '2026-06-15 22:00:00', 1, 'H', FALSE),
    (gen_random_uuid(), '37', v_espanha,      v_arabia,        '2026-06-21 16:00:00', 1, 'H', FALSE),
    (gen_random_uuid(), '39', v_uruguai,      v_cabo_verde,    '2026-06-21 22:00:00', 1, 'H', FALSE),
    (gen_random_uuid(), '63', v_cabo_verde,   v_arabia,        '2026-06-27 00:00:00', 1, 'H', FALSE),
    (gen_random_uuid(), '64', v_uruguai,      v_espanha,       '2026-06-27 00:00:00', 1, 'H', FALSE),
    -- GRUPO I
    (gen_random_uuid(), '17', v_franca,       v_senegal,       '2026-06-16 19:00:00', 1, 'I', FALSE),
    (gen_random_uuid(), '18', v_iraque,       v_noruega,       '2026-06-16 22:00:00', 1, 'I', FALSE),
    (gen_random_uuid(), '42', v_franca,       v_iraque,        '2026-06-22 21:00:00', 1, 'I', FALSE),
    (gen_random_uuid(), '43', v_noruega,      v_senegal,       '2026-06-23 00:00:00', 1, 'I', FALSE),
    (gen_random_uuid(), '61', v_noruega,      v_franca,        '2026-06-26 19:00:00', 1, 'I', FALSE),
    (gen_random_uuid(), '62', v_senegal,      v_iraque,        '2026-06-26 19:00:00', 1, 'I', FALSE),
    -- GRUPO J
    (gen_random_uuid(), '19', v_argentina,    v_argelia,       '2026-06-17 01:00:00', 1, 'J', FALSE),
    (gen_random_uuid(), '20', v_austria,      v_jordania,      '2026-06-17 04:00:00', 1, 'J', FALSE),
    (gen_random_uuid(), '41', v_argentina,    v_austria,       '2026-06-22 17:00:00', 1, 'J', FALSE),
    (gen_random_uuid(), '44', v_jordania,     v_argelia,       '2026-06-23 03:00:00', 1, 'J', FALSE),
    (gen_random_uuid(), '71', v_argelia,      v_austria,       '2026-06-28 02:00:00', 1, 'J', FALSE),
    (gen_random_uuid(), '72', v_jordania,     v_argentina,     '2026-06-28 02:00:00', 1, 'J', FALSE),
    -- GRUPO K
    (gen_random_uuid(), '21', v_portugal,     v_rd_congo,      '2026-06-17 17:00:00', 1, 'K', FALSE),
    (gen_random_uuid(), '24', v_uzbequistao,  v_colombia,      '2026-06-18 02:00:00', 1, 'K', FALSE),
    (gen_random_uuid(), '45', v_portugal,     v_uzbequistao,   '2026-06-23 17:00:00', 1, 'K', FALSE),
    (gen_random_uuid(), '48', v_colombia,     v_rd_congo,      '2026-06-24 02:00:00', 1, 'K', FALSE),
    (gen_random_uuid(), '69', v_colombia,     v_portugal,      '2026-06-27 23:30:00', 1, 'K', FALSE),
    (gen_random_uuid(), '70', v_rd_congo,     v_uzbequistao,   '2026-06-27 23:30:00', 1, 'K', FALSE),
    -- GRUPO L
    (gen_random_uuid(), '22', v_inglaterra,   v_croacia,       '2026-06-17 20:00:00', 1, 'L', FALSE),
    (gen_random_uuid(), '23', v_gana,         v_panama,        '2026-06-17 23:00:00', 1, 'L', FALSE),
    (gen_random_uuid(), '46', v_inglaterra,   v_gana,          '2026-06-23 20:00:00', 1, 'L', FALSE),
    (gen_random_uuid(), '47', v_panama,       v_croacia,       '2026-06-23 23:00:00', 1, 'L', FALSE),
    (gen_random_uuid(), '67', v_panama,       v_inglaterra,    '2026-06-27 21:00:00', 1, 'L', FALSE),
    (gen_random_uuid(), '68', v_croacia,      v_gana,          '2026-06-27 21:00:00', 1, 'L', FALSE);

  -- ============================================================
  -- 32 JOGOS — MATA-MATA (times a definir)
  -- ============================================================
  INSERT INTO "Matches" ("Id", "FifaMatchCode", "HomeTeamId", "AwayTeamId", "KickoffAt", "Stage", "GroupName", "IsFinished") VALUES
    -- 32 avos de final (Stage=2)
    (gen_random_uuid(), '73', v_tbd, v_tbd, '2026-06-28 19:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '74', v_tbd, v_tbd, '2026-06-29 20:30:00', 2, NULL, FALSE),
    (gen_random_uuid(), '75', v_tbd, v_tbd, '2026-06-30 01:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '76', v_tbd, v_tbd, '2026-06-29 17:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '77', v_tbd, v_tbd, '2026-06-30 21:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '78', v_tbd, v_tbd, '2026-06-30 17:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '79', v_tbd, v_tbd, '2026-07-01 01:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '80', v_tbd, v_tbd, '2026-07-01 16:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '81', v_tbd, v_tbd, '2026-07-02 00:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '82', v_tbd, v_tbd, '2026-07-01 20:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '83', v_tbd, v_tbd, '2026-07-02 23:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '84', v_tbd, v_tbd, '2026-07-02 19:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '85', v_tbd, v_tbd, '2026-07-03 03:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '86', v_tbd, v_tbd, '2026-07-03 22:00:00', 2, NULL, FALSE),
    (gen_random_uuid(), '87', v_tbd, v_tbd, '2026-07-04 01:30:00', 2, NULL, FALSE),
    (gen_random_uuid(), '88', v_tbd, v_tbd, '2026-07-03 18:00:00', 2, NULL, FALSE),
    -- Oitavas de final (Stage=3)
    (gen_random_uuid(), '89', v_tbd, v_tbd, '2026-07-04 21:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '90', v_tbd, v_tbd, '2026-07-04 17:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '91', v_tbd, v_tbd, '2026-07-05 20:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '92', v_tbd, v_tbd, '2026-07-06 00:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '93', v_tbd, v_tbd, '2026-07-06 19:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '94', v_tbd, v_tbd, '2026-07-07 00:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '95', v_tbd, v_tbd, '2026-07-07 16:00:00', 3, NULL, FALSE),
    (gen_random_uuid(), '96', v_tbd, v_tbd, '2026-07-07 20:00:00', 3, NULL, FALSE),
    -- Quartas de final (Stage=4)
    (gen_random_uuid(), '97',  v_tbd, v_tbd, '2026-07-09 20:00:00', 4, NULL, FALSE),
    (gen_random_uuid(), '98',  v_tbd, v_tbd, '2026-07-10 19:00:00', 4, NULL, FALSE),
    (gen_random_uuid(), '99',  v_tbd, v_tbd, '2026-07-11 21:00:00', 4, NULL, FALSE),
    (gen_random_uuid(), '100', v_tbd, v_tbd, '2026-07-12 01:00:00', 4, NULL, FALSE),
    -- Semifinais (Stage=5)
    (gen_random_uuid(), '101', v_tbd, v_tbd, '2026-07-14 19:00:00', 5, NULL, FALSE),
    (gen_random_uuid(), '102', v_tbd, v_tbd, '2026-07-15 19:00:00', 5, NULL, FALSE),
    -- Terceiro lugar (Stage=6)
    (gen_random_uuid(), '103', v_tbd, v_tbd, '2026-07-18 21:00:00', 6, NULL, FALSE),
    -- Final (Stage=7)
    (gen_random_uuid(), '104', v_tbd, v_tbd, '2026-07-19 19:00:00', 7, NULL, FALSE);

END;
$$;

-- Verificação rápida
SELECT COUNT(*) AS total_times  FROM "Teams";
SELECT COUNT(*) AS total_jogos  FROM "Matches";
SELECT "Stage", COUNT(*) AS qtd FROM "Matches" GROUP BY "Stage" ORDER BY "Stage";
