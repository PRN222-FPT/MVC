SELECT 'CREATE DATABASE prn222'
WHERE NOT EXISTS (
    SELECT FROM pg_database WHERE datname = 'prn222'
)\gexec

\c prn222

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE "benchmark_results" (
	"result_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"benchmark_run_id" uuid NOT NULL,
	"question_id" uuid,
	"score" numeric(5, 2),
	"response_time_ms" integer,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "benchmark_runs" (
	"benchmark_run_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"run_name" varchar(255),
	"executed_by" uuid,
	"started_at" timestamp DEFAULT CURRENT_TIMESTAMP,
	"completed_at" timestamp
);
CREATE TABLE "chapters" (
	"chapter_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"subject_id" uuid NOT NULL,
	"chapter_title" varchar(255) NOT NULL,
	"chapter_order" integer DEFAULT 1,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "chunks" (
	"chunk_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"document_id" uuid NOT NULL,
	"chunk_index" integer NOT NULL,
	"content" text NOT NULL,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "documents" (
	"document_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"chapter_id" uuid NOT NULL,
	"title" varchar(255) NOT NULL,
	"file_url" text NOT NULL,
	"file_type" varchar(50),
	"uploaded_by" uuid,
	"uploaded_teacher" uuid,
	"status" varchar(50) DEFAULT 'pending',
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP,
	"subject_id" uuid NOT NULL
);
CREATE TABLE "messages" (
	"message_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"session_id" uuid NOT NULL,
	"sender_role" varchar(50) NOT NULL,
	"message_content" text NOT NULL,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "processing_jobs" (
	"job_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"document_id" uuid NOT NULL,
	"job_status" varchar(50) DEFAULT 'queued',
	"started_at" timestamp,
	"finished_at" timestamp,
	"error_message" text
);
CREATE TABLE "sessions" (
	"session_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"user_id" uuid NOT NULL,
	"started_at" timestamp DEFAULT CURRENT_TIMESTAMP,
	"ended_at" timestamp
);
CREATE TABLE "subjects" (
	"subject_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"subject_code" varchar(50) NOT NULL,
	"subject_name" varchar(255) NOT NULL,
	"description" text,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "teacher_subjects" (
	"teacher_subject_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"teacher_id" uuid NOT NULL UNIQUE,
	"subject_id" uuid NOT NULL UNIQUE,
	"is_head_of_department" boolean DEFAULT false NOT NULL,
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP,
	CONSTRAINT "teacher_subjects_teacher_subject_key" UNIQUE("teacher_id","subject_id")
);
CREATE TABLE "teachers" (
	"teacher_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"full_name" varchar(255) NOT NULL,
	"email" varchar(255),
	"department" varchar(255),
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "test_questions" (
	"question_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"chapter_id" uuid NOT NULL,
	"question_text" text NOT NULL,
	"difficulty" varchar(50),
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE "users" (
	"user_id" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
	"full_name" varchar(255) NOT NULL,
	"email" varchar(255) NOT NULL,
	"password_hash" text NOT NULL,
	"role" varchar(50) DEFAULT 'student',
	"created_at" timestamp DEFAULT CURRENT_TIMESTAMP,
	"is_blocked" boolean DEFAULT false NOT NULL,
	"student_code" varchar(50)
);
CREATE UNIQUE INDEX "PK___EFMigrationsHistory" ON "__EFMigrationsHistory" ("MigrationId");
CREATE UNIQUE INDEX "benchmark_results_pkey" ON "benchmark_results" ("result_id");
CREATE INDEX "IX_benchmark_results_benchmark_run_id" ON "benchmark_results" ("benchmark_run_id");
CREATE INDEX "IX_benchmark_results_question_id" ON "benchmark_results" ("question_id");
CREATE UNIQUE INDEX "benchmark_runs_pkey" ON "benchmark_runs" ("benchmark_run_id");
CREATE INDEX "IX_benchmark_runs_executed_by" ON "benchmark_runs" ("executed_by");
CREATE UNIQUE INDEX "chapters_pkey" ON "chapters" ("chapter_id");
CREATE INDEX "idx_chapters_subject" ON "chapters" ("subject_id");
CREATE UNIQUE INDEX "chunks_pkey" ON "chunks" ("chunk_id");
CREATE INDEX "idx_chunks_document" ON "chunks" ("document_id");
CREATE UNIQUE INDEX "documents_pkey" ON "documents" ("document_id");
CREATE INDEX "idx_documents_chapter" ON "documents" ("chapter_id");
CREATE INDEX "idx_documents_subject" ON "documents" ("subject_id");
CREATE INDEX "IX_documents_uploaded_by" ON "documents" ("uploaded_by");
CREATE INDEX "IX_documents_uploaded_teacher" ON "documents" ("uploaded_teacher");
CREATE INDEX "idx_messages_session" ON "messages" ("session_id");
CREATE UNIQUE INDEX "messages_pkey" ON "messages" ("message_id");
CREATE INDEX "idx_processing_document" ON "processing_jobs" ("document_id");
CREATE UNIQUE INDEX "processing_jobs_pkey" ON "processing_jobs" ("job_id");
CREATE INDEX "IX_sessions_user_id" ON "sessions" ("user_id");
CREATE UNIQUE INDEX "sessions_pkey" ON "sessions" ("session_id");
CREATE UNIQUE INDEX "subjects_pkey" ON "subjects" ("subject_id");
CREATE UNIQUE INDEX "subjects_subject_code_key" ON "subjects" ("subject_code");
CREATE INDEX "idx_teacher_subjects_subject" ON "teacher_subjects" ("subject_id");
CREATE INDEX "idx_teacher_subjects_teacher" ON "teacher_subjects" ("teacher_id");
CREATE UNIQUE INDEX "teacher_subjects_pkey" ON "teacher_subjects" ("teacher_subject_id");
CREATE UNIQUE INDEX "teacher_subjects_teacher_subject_key" ON "teacher_subjects" ("teacher_id","subject_id");
CREATE UNIQUE INDEX "teachers_email_key" ON "teachers" ("email");
CREATE UNIQUE INDEX "teachers_pkey" ON "teachers" ("teacher_id");
CREATE INDEX "idx_questions_chapter" ON "test_questions" ("chapter_id");
CREATE UNIQUE INDEX "test_questions_pkey" ON "test_questions" ("question_id");
CREATE UNIQUE INDEX "users_email_key" ON "users" ("email");
CREATE UNIQUE INDEX "users_pkey" ON "users" ("user_id");
CREATE UNIQUE INDEX "users_student_code_key" ON "users" ("student_code");
ALTER TABLE "benchmark_results" ADD CONSTRAINT "fk_result_question" FOREIGN KEY ("question_id") REFERENCES "test_questions"("question_id") ON DELETE SET NULL;
ALTER TABLE "benchmark_results" ADD CONSTRAINT "fk_result_run" FOREIGN KEY ("benchmark_run_id") REFERENCES "benchmark_runs"("benchmark_run_id") ON DELETE CASCADE;
ALTER TABLE "benchmark_runs" ADD CONSTRAINT "fk_benchmark_user" FOREIGN KEY ("executed_by") REFERENCES "users"("user_id") ON DELETE SET NULL;
ALTER TABLE "chapters" ADD CONSTRAINT "fk_chapter_subject" FOREIGN KEY ("subject_id") REFERENCES "subjects"("subject_id") ON DELETE CASCADE;
ALTER TABLE "chunks" ADD CONSTRAINT "fk_chunk_document" FOREIGN KEY ("document_id") REFERENCES "documents"("document_id") ON DELETE CASCADE;
ALTER TABLE "documents" ADD CONSTRAINT "fk_document_chapter" FOREIGN KEY ("chapter_id") REFERENCES "chapters"("chapter_id") ON DELETE CASCADE;
ALTER TABLE "documents" ADD CONSTRAINT "fk_document_subject" FOREIGN KEY ("subject_id") REFERENCES "subjects"("subject_id");
ALTER TABLE "documents" ADD CONSTRAINT "fk_document_teacher" FOREIGN KEY ("uploaded_teacher") REFERENCES "teachers"("teacher_id") ON DELETE SET NULL;
ALTER TABLE "documents" ADD CONSTRAINT "fk_document_user" FOREIGN KEY ("uploaded_by") REFERENCES "users"("user_id") ON DELETE SET NULL;
ALTER TABLE "messages" ADD CONSTRAINT "fk_message_session" FOREIGN KEY ("session_id") REFERENCES "sessions"("session_id") ON DELETE CASCADE;
ALTER TABLE "processing_jobs" ADD CONSTRAINT "fk_processing_document" FOREIGN KEY ("document_id") REFERENCES "documents"("document_id") ON DELETE CASCADE;
ALTER TABLE "sessions" ADD CONSTRAINT "fk_session_user" FOREIGN KEY ("user_id") REFERENCES "users"("user_id") ON DELETE CASCADE;
ALTER TABLE "teacher_subjects" ADD CONSTRAINT "fk_teacher_subject_subject" FOREIGN KEY ("subject_id") REFERENCES "subjects"("subject_id") ON DELETE CASCADE;
ALTER TABLE "teacher_subjects" ADD CONSTRAINT "fk_teacher_subject_teacher" FOREIGN KEY ("teacher_id") REFERENCES "teachers"("teacher_id") ON DELETE CASCADE;
ALTER TABLE "test_questions" ADD CONSTRAINT "fk_question_chapter" FOREIGN KEY ("chapter_id") REFERENCES "chapters"("chapter_id") ON DELETE CASCADE;