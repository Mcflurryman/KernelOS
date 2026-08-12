CREATE TABLE memory_documents (
    id TEXT PRIMARY KEY NOT NULL,
    knowledge_document_id TEXT NOT NULL UNIQUE,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    version_number INTEGER NOT NULL,
    version_updated_at_utc TEXT NOT NULL,
    version_content_hash TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    mime_type TEXT NOT NULL,
    format TEXT NULL,
    language TEXT NULL
);
CREATE TABLE memory_document_metadata (
    document_id TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (document_id, key),
    FOREIGN KEY (document_id) REFERENCES memory_documents(id) ON DELETE CASCADE
);
CREATE TABLE memory_items (
    document_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL,
    item_id TEXT NOT NULL,
    knowledge_item_id TEXT NOT NULL,
    item_type INTEGER NOT NULL,
    content TEXT NOT NULL COLLATE BINARY,
    content_hash TEXT NOT NULL,
    mime_type TEXT NOT NULL,
    format TEXT NULL,
    language TEXT NULL,
    source_document_id TEXT NOT NULL,
    safe_reference TEXT NOT NULL,
    display_reference TEXT NOT NULL,
    locator_section_id TEXT NULL,
    locator_line INTEGER NULL,
    locator_column INTEGER NULL,
    locator_row INTEGER NULL,
    locator_json_path TEXT NULL,
    locator_description TEXT NULL,
    PRIMARY KEY (document_id, ordinal),
    FOREIGN KEY (document_id) REFERENCES memory_documents(id) ON DELETE CASCADE
);
CREATE TABLE memory_item_metadata (
    document_id TEXT NOT NULL,
    item_ordinal INTEGER NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (document_id, item_ordinal, key),
    FOREIGN KEY (document_id, item_ordinal) REFERENCES memory_items(document_id, ordinal) ON DELETE CASCADE
);
CREATE INDEX ix_memory_documents_updated ON memory_documents(updated_at_utc DESC, id ASC);
CREATE INDEX ix_memory_documents_content_hash ON memory_documents(content_hash);
CREATE INDEX ix_memory_items_item_id ON memory_items(item_id);
CREATE INDEX ix_memory_items_content_hash ON memory_items(content_hash);
CREATE INDEX ix_memory_items_type ON memory_items(item_type);
CREATE INDEX ix_memory_document_metadata_key_value ON memory_document_metadata(key, value, document_id);
