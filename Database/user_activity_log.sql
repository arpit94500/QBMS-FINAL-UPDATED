-- Activity log of application users (also created automatically on first use by ActivityLogRepository).
CREATE TABLE IF NOT EXISTS user_activity_log
(
    log_id        BIGSERIAL PRIMARY KEY,
    logged_at     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    user_id       BIGINT NULL,
    personal_number VARCHAR(50) NULL,
    user_name     VARCHAR(200) NULL,
    role_name     VARCHAR(50) NULL,
    activity      VARCHAR(200) NOT NULL,   -- Controller/Action or LOGIN_SUCCESS / LOGIN_FAILED
    http_method   VARCHAR(10) NULL,
    path          VARCHAR(500) NULL,
    query_string  VARCHAR(1000) NULL,
    ip_address    VARCHAR(64) NULL,
    is_success    BOOLEAN NOT NULL DEFAULT TRUE,
    details       VARCHAR(1000) NULL
);
CREATE INDEX IF NOT EXISTS ix_user_activity_log_time ON user_activity_log (logged_at DESC);
CREATE INDEX IF NOT EXISTS ix_user_activity_log_user ON user_activity_log (personal_number);

-- The application user needs rights on the table and its sequence. Replace qbms_app with the user in your connection string.
-- GRANT SELECT, INSERT ON user_activity_log TO qbms_app;
-- GRANT USAGE, SELECT ON SEQUENCE user_activity_log_log_id_seq TO qbms_app;
