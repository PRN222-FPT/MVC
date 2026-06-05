ALTER TABLE users
    ADD COLUMN IF NOT EXISTS student_code character varying(50);

CREATE UNIQUE INDEX IF NOT EXISTS users_student_code_key
    ON users (student_code)
    WHERE student_code IS NOT NULL;
