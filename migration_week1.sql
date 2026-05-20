-- Migration Week 1 — Bolão Copa 2026
-- Run once in Supabase SQL Editor

-- 1. Add Status to Matches (1=Scheduled, 2=Locked, 3=Live, 4=Finished, 5=Calculated)
ALTER TABLE "Matches"
    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 1;

-- 2. Add Status to Predictions (1=Saved, 2=Locked, 3=WaitingResult, 4=Scored)
ALTER TABLE "Predictions"
    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 1;

-- 3. Add IsAdmin to Users
ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "IsAdmin" boolean NOT NULL DEFAULT FALSE;

-- Verification
SELECT
    (SELECT COUNT(*) FROM "Matches" WHERE "Status" IS NOT NULL)   AS matches_with_status,
    (SELECT COUNT(*) FROM "Predictions" WHERE "Status" IS NOT NULL) AS predictions_with_status,
    (SELECT COUNT(*) FROM "Users" WHERE "IsAdmin" IS NOT NULL)     AS users_with_is_admin;
