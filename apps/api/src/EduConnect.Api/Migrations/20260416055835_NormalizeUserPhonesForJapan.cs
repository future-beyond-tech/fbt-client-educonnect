using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserPhonesForJapan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        WITH normalized AS (
                            SELECT
                                id,
                                school_id,
                                CASE
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{11}$'
                                        THEN regexp_replace(phone, '\D', '', 'g')
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^81\d{11}$'
                                        THEN substring(regexp_replace(phone, '\D', '', 'g') from 3)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^91\d{10}$'
                                        THEN '0' || right(regexp_replace(phone, '\D', '', 'g'), 10)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                        THEN '0' || regexp_replace(phone, '\D', '', 'g')
                                    ELSE NULL
                                END AS normalized_phone
                            FROM users
                        )
                        SELECT 1
                        FROM normalized
                        WHERE normalized_phone IS NULL
                           OR normalized_phone !~ '^\d{11}$'
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate users.phone to Japan format because one or more rows cannot be normalized to an 11-digit value.';
                    END IF;

                    IF EXISTS (
                        WITH normalized AS (
                            SELECT
                                school_id,
                                CASE
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{11}$'
                                        THEN regexp_replace(phone, '\D', '', 'g')
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^81\d{11}$'
                                        THEN substring(regexp_replace(phone, '\D', '', 'g') from 3)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^91\d{10}$'
                                        THEN '0' || right(regexp_replace(phone, '\D', '', 'g'), 10)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                        THEN '0' || regexp_replace(phone, '\D', '', 'g')
                                    ELSE NULL
                                END AS normalized_phone
                            FROM users
                        )
                        SELECT 1
                        FROM normalized
                        GROUP BY school_id, normalized_phone
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate users.phone to Japan format because duplicate phone numbers would be created inside the same school.';
                    END IF;

                    UPDATE users AS current_users
                    SET phone = normalized.normalized_phone
                    FROM (
                        SELECT
                            id,
                            CASE
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{11}$'
                                    THEN regexp_replace(phone, '\D', '', 'g')
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^81\d{11}$'
                                    THEN substring(regexp_replace(phone, '\D', '', 'g') from 3)
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^91\d{10}$'
                                    THEN '0' || right(regexp_replace(phone, '\D', '', 'g'), 10)
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                    THEN '0' || regexp_replace(phone, '\D', '', 'g')
                                ELSE regexp_replace(phone, '\D', '', 'g')
                            END AS normalized_phone
                        FROM users
                    ) AS normalized
                    WHERE current_users.id = normalized.id
                      AND current_users.phone IS DISTINCT FROM normalized.normalized_phone;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        WITH normalized AS (
                            SELECT
                                id,
                                school_id,
                                CASE
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^0\d{10}$'
                                        THEN substring(regexp_replace(phone, '\D', '', 'g') from 2)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                        THEN regexp_replace(phone, '\D', '', 'g')
                                    ELSE NULL
                                END AS legacy_phone
                            FROM users
                        )
                        SELECT 1
                        FROM normalized
                        WHERE legacy_phone IS NULL
                           OR legacy_phone !~ '^\d{10}$'
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert users.phone from Japan format because one or more rows cannot be converted back to a 10-digit legacy value.';
                    END IF;

                    IF EXISTS (
                        WITH normalized AS (
                            SELECT
                                school_id,
                                CASE
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^0\d{10}$'
                                        THEN substring(regexp_replace(phone, '\D', '', 'g') from 2)
                                    WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                        THEN regexp_replace(phone, '\D', '', 'g')
                                    ELSE NULL
                                END AS legacy_phone
                            FROM users
                        )
                        SELECT 1
                        FROM normalized
                        GROUP BY school_id, legacy_phone
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert users.phone from Japan format because duplicate legacy phone numbers would be created inside the same school.';
                    END IF;

                    UPDATE users AS current_users
                    SET phone = normalized.legacy_phone
                    FROM (
                        SELECT
                            id,
                            CASE
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^0\d{10}$'
                                    THEN substring(regexp_replace(phone, '\D', '', 'g') from 2)
                                WHEN regexp_replace(phone, '\D', '', 'g') ~ '^\d{10}$'
                                    THEN regexp_replace(phone, '\D', '', 'g')
                                ELSE regexp_replace(phone, '\D', '', 'g')
                            END AS legacy_phone
                        FROM users
                    ) AS normalized
                    WHERE current_users.id = normalized.id
                      AND current_users.phone IS DISTINCT FROM normalized.legacy_phone;
                END $$;
                """);
        }
    }
}
