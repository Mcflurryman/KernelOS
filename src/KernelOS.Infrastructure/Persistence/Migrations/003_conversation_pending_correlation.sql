CREATE TABLE conversation_pending_executions (
    pending_execution_id TEXT PRIMARY KEY NOT NULL,
    conversation_id TEXT NOT NULL,
    user_message_id TEXT NOT NULL,
    assistant_message_id TEXT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE,
    FOREIGN KEY (user_message_id) REFERENCES conversation_messages(id) ON DELETE CASCADE,
    FOREIGN KEY (assistant_message_id) REFERENCES conversation_messages(id) ON DELETE SET NULL
);
CREATE INDEX ix_conversation_pending_executions_conversation_created
    ON conversation_pending_executions(conversation_id, created_at_utc DESC, pending_execution_id ASC);
