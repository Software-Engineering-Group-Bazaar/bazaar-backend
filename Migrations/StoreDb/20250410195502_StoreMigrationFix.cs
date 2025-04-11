using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace bazaar.Migrations.StoreDb
{
    /// <inheritdoc />
    public partial class StoreMigrationFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the foreign key constraint
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        DROP CONSTRAINT ""FK_Stores_StoreCategories_categoryid"";
    ");

            // Alter categoryid in Stores table from uuid to integer
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        ALTER COLUMN ""categoryid"" TYPE integer 
        USING (categoryid::text::integer);
    ");

            // Alter id in StoreCategories table from uuid to integer
            migrationBuilder.Sql(@"
        ALTER TABLE ""StoreCategories"" 
        ALTER COLUMN ""id"" TYPE integer 
        USING (id::text::integer);
    ");

            // Recreate the foreign key constraint
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        ADD CONSTRAINT ""FK_Stores_StoreCategories_categoryid"" 
        FOREIGN KEY (""categoryid"") REFERENCES ""StoreCategories""(""id"");
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the foreign key constraint
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        DROP CONSTRAINT ""FK_Stores_StoreCategories_categoryid"";
    ");

            // Revert id in StoreCategories table from integer to uuid
            migrationBuilder.Sql(@"
        ALTER TABLE ""StoreCategories"" 
        ALTER COLUMN ""id"" TYPE uuid 
        USING (id::text::uuid);
    ");

            // Revert categoryid in Stores table from integer to uuid
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        ALTER COLUMN ""categoryid"" TYPE uuid 
        USING (categoryid::text::uuid);
    ");

            // Recreate the foreign key constraint
            migrationBuilder.Sql(@"
        ALTER TABLE ""Stores"" 
        ADD CONSTRAINT ""FK_Stores_StoreCategories_categoryid"" 
        FOREIGN KEY (""categoryid"") REFERENCES ""StoreCategories""(""id"");
    ");
        }
    }
}

