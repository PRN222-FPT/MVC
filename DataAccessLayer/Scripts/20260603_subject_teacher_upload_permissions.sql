CREATE TABLE IF NOT EXISTS teacher_subjects (
    teacher_subject_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    teacher_id uuid NOT NULL,
    subject_id uuid NOT NULL,
    is_head_of_department boolean NOT NULL DEFAULT false,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_teacher_subject_teacher
        FOREIGN KEY (teacher_id) REFERENCES teachers(teacher_id) ON DELETE CASCADE,
    CONSTRAINT fk_teacher_subject_subject
        FOREIGN KEY (subject_id) REFERENCES subjects(subject_id) ON DELETE CASCADE,
    CONSTRAINT teacher_subjects_teacher_subject_key UNIQUE (teacher_id, subject_id)
);

CREATE INDEX IF NOT EXISTS idx_teacher_subjects_teacher
    ON teacher_subjects(teacher_id);

CREATE INDEX IF NOT EXISTS idx_teacher_subjects_subject
    ON teacher_subjects(subject_id);

ALTER TABLE documents
ADD COLUMN IF NOT EXISTS subject_id uuid;

UPDATE documents AS document
SET subject_id = chapter.subject_id
FROM chapters AS chapter
WHERE document.chapter_id = chapter.chapter_id
  AND document.subject_id IS NULL;

ALTER TABLE documents
ALTER COLUMN subject_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_document_subject'
    ) THEN
        ALTER TABLE documents
        ADD CONSTRAINT fk_document_subject
            FOREIGN KEY (subject_id) REFERENCES subjects(subject_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_documents_subject
    ON documents(subject_id);
