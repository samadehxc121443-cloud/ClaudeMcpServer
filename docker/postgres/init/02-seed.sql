-- Seed data for local development and demos.
-- Runs once via /docker-entrypoint-initdb.d/ when the Postgres data volume is empty.
-- Idempotent: ON CONFLICT on the unique "Key" index makes re-runs harmless.

-- Plans first: license keys reference them. Limits are parametrization data
-- and live here, in the database — never in env vars or config.
INSERT INTO "Plans" ("Name", "Price", "MaxEmailsPerDay", "DurationDays", "IsActive", "CreatedAt")
VALUES
    ('Free', 0.00,  100, 30,   TRUE, NOW()),
    ('Pro',  9.99, 1000, NULL, TRUE, NOW())
ON CONFLICT ("Name") DO NOTHING;

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

-- Link the demo keys to their plan rows by name.
UPDATE "LicenseKeys"
SET "PlanId" = (SELECT "Id" FROM "Plans" WHERE "Plans"."Name" = "LicenseKeys"."PlanName")
WHERE "PlanId" IS NULL AND "PlanName" IN ('Free', 'Pro');

-- Admin keys live in the database (never in env vars or config).
-- With a seeded key present, the app skips its bootstrap-key generation.
INSERT INTO "AdminKeys" ("Key", "Name", "IsActive", "CreatedAt")
VALUES
    ('demo-admin-99999999999999999999',
     'Demo Admin (seeded)',
     TRUE, NOW())
ON CONFLICT ("Key") DO NOTHING;
