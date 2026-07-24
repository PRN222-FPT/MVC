CREATE TABLE IF NOT EXISTS "chunking_settings" (
    "id" smallint PRIMARY KEY DEFAULT 1,
    "chunk_size_characters" integer NOT NULL DEFAULT 1400,
    "updated_at" timestamp DEFAULT CURRENT_TIMESTAMP,
    "updated_by" uuid,
    CONSTRAINT "chunking_settings_singleton" CHECK ("id" = 1),
    CONSTRAINT "fk_chunking_settings_user" FOREIGN KEY ("updated_by")
        REFERENCES "users" ("user_id") ON DELETE SET NULL
);

INSERT INTO "chunking_settings" ("id", "chunk_size_characters")
VALUES (1, 1400)
ON CONFLICT ("id") DO NOTHING;
