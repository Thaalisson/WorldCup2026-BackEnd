-- ============================================================
-- Auditoria de PALPITES — registra toda criação/edição/remoção
-- de palpite (valor antigo → novo, quando, e qual usuário do banco).
-- Idempotente: pode rodar de novo sem problema.
-- (Também disponível via: dotnet run -- setup-audit)
-- ============================================================

CREATE TABLE IF NOT EXISTS prediction_audit (
  id            bigserial PRIMARY KEY,
  prediction_id uuid,
  pool_id       uuid,
  user_id       uuid,
  match_id      uuid,
  operation     text,
  old_home      int,
  old_away      int,
  new_home      int,
  new_away      int,
  old_points    int,
  new_points    int,
  db_user       text,
  changed_at    timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION fn_prediction_audit() RETURNS trigger AS $$
BEGIN
  IF (TG_OP = 'DELETE') THEN
    INSERT INTO prediction_audit(prediction_id, pool_id, user_id, match_id, operation,
        old_home, old_away, new_home, new_away, old_points, new_points, db_user)
    VALUES (OLD."Id", OLD."PoolId", OLD."UserId", OLD."MatchId", 'DELETE',
        OLD."HomeScorePrediction", OLD."AwayScorePrediction", NULL, NULL,
        OLD."PointsEarned", NULL, current_user);
    RETURN OLD;

  ELSIF (TG_OP = 'UPDATE') THEN
    -- só registra quando o PALPITE muda (ignora recálculo de pontos)
    IF (OLD."HomeScorePrediction" IS DISTINCT FROM NEW."HomeScorePrediction"
        OR OLD."AwayScorePrediction" IS DISTINCT FROM NEW."AwayScorePrediction") THEN
      INSERT INTO prediction_audit(prediction_id, pool_id, user_id, match_id, operation,
          old_home, old_away, new_home, new_away, old_points, new_points, db_user)
      VALUES (NEW."Id", NEW."PoolId", NEW."UserId", NEW."MatchId", 'UPDATE',
          OLD."HomeScorePrediction", OLD."AwayScorePrediction",
          NEW."HomeScorePrediction", NEW."AwayScorePrediction",
          OLD."PointsEarned", NEW."PointsEarned", current_user);
    END IF;
    RETURN NEW;

  ELSIF (TG_OP = 'INSERT') THEN
    INSERT INTO prediction_audit(prediction_id, pool_id, user_id, match_id, operation,
        old_home, old_away, new_home, new_away, old_points, new_points, db_user)
    VALUES (NEW."Id", NEW."PoolId", NEW."UserId", NEW."MatchId", 'INSERT',
        NULL, NULL, NEW."HomeScorePrediction", NEW."AwayScorePrediction",
        NULL, NEW."PointsEarned", current_user);
    RETURN NEW;
  END IF;
  RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_prediction_audit ON "Predictions";
CREATE TRIGGER trg_prediction_audit
AFTER INSERT OR UPDATE OR DELETE ON "Predictions"
FOR EACH ROW EXECUTE FUNCTION fn_prediction_audit();
