CREATE TABLE conversations (
    id TEXT PRIMARY KEY NOT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number > 0)
);
CREATE TABLE conversation_messages (
    id TEXT PRIMARY KEY NOT NULL,
    conversation_id TEXT NOT NULL,
    sequence_number INTEGER NOT NULL CHECK (sequence_number > 0),
    role INTEGER NOT NULL CHECK (role IN (0, 1)),
    content TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    UNIQUE (conversation_id, sequence_number),
    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
);
CREATE INDEX ix_conversations_updated ON conversations(updated_at_utc DESC, id ASC);
CREATE INDEX ix_conversation_messages_conversation_sequence ON conversation_messages(conversation_id, sequence_number);
