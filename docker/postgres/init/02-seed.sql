-- Seed data for local development and demos.
-- Runs once via /docker-entrypoint-initdb.d/ when the Postgres data volume is empty.
-- Idempotent: ON CONFLICT on the unique "Key" index makes re-runs harmless.

INSERT INTO "LicenseKeys"
    ("Key", "ClientName", "Notes", "PlanName", "IsActive", "CreatedAt", "ExpiresAt")
VALUES
    ('demo-pro-1111111111111111111111',
     'Demo Pro Client',
     'Seeded by docker-entrypoint-initdb.d',
     'Pro',  TRUE,  NOW(), NULL),

    ('demo-free-222222222222222222222',
     'Demo Free Client',
     'Seeded by docker-entrypoint-initdb.d',
     'Free', TRUE,  NOW(), NOW() + INTERVAL '30 days'),

    ('demo-expired-3333333333333333333',
     'Demo Expired Client',
     'Seeded by docker-entrypoint-initdb.d — validation must fail with "expired"',
     'Free', TRUE,  NOW() - INTERVAL '60 days', NOW() - INTERVAL '30 days'),

    ('demo-revoked-4444444444444444444',
     'Demo Revoked Client',
     'Seeded by docker-entrypoint-initdb.d — validation must fail with "revoked"',
     'Pro',  FALSE, NOW(), NULL)
ON CONFLICT ("Key") DO NOTHING;

-- Admin keys live in the database (never in env vars or config).
-- With a seeded key present, the app skips its bootstrap-key generation.
INSERT INTO "AdminKeys" ("Key", "Name", "IsActive", "CreatedAt")
VALUES
    ('demo-admin-99999999999999999999',
     'Demo Admin (seeded)',
     TRUE, NOW())
ON CONFLICT ("Key") DO NOTHING;
