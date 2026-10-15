-- ============================================================================
-- Enhanced CD Inventory Database Schema
-- Optimized for both Classical and Popular Music
-- ============================================================================

BEGIN TRANSACTION;

-- ----------------------------------------------------------------------------
-- Core Tables
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "albums" (
    "UPC"           TEXT PRIMARY KEY,
    "Artist"        TEXT NOT NULL,          -- Display name for album artist
    "Title"         TEXT NOT NULL, 
    "Year"          TEXT,                   -- Release/recording year
    "Format"        TEXT,                   -- CD, Vinyl, Digital, SACD, etc.
    "Label"         TEXT,
    "Genre"         TEXT,
    "Styles"        TEXT,
    "SortArtist"    TEXT, 

    -- Enhanced fields for classical music (NULL for popular music)
    "Composer"      TEXT,                   -- NULL or same as Artist for pop
    "Performer"     TEXT,                   -- NULL or same as Artist for pop
    "Conductor"     TEXT,                   -- NULL for non-classical
    "Ensemble"      TEXT,                   -- Orchestra, Quartet, etc.

    -- Additional metadata
    "Country"       TEXT,                   -- Country of release
    "CatalogNumber" TEXT,                   -- Label catalog number
    "Barcode"       TEXT,                   -- EAN/UPC barcode
    "MediaCount"    INTEGER DEFAULT 1,      -- Number of discs 
    "Notes"         TEXT                    -- Free-form notes
);

CREATE TABLE IF NOT EXISTS "tracks" (
    "TrackID"           INTEGER PRIMARY KEY AUTOINCREMENT,
    "UPC"               TEXT NOT NULL,
    "Position"          TEXT NOT NULL,          -- 1, 2, A1, B2, 1-1, etc.
    "Track_Title"       TEXT NOT NULL,
    "Duration"          TEXT,                   -- MM:SS or HH:MM:SS format

    -- Mult-movement work support
    "Parent_Work"       TEXT,                   -- Title of larger work
    "Movement_Number"   INTEGER,                -- 1, 2, 3, etc.
    "Movement_Tempo"    TEXT,                   -- Allegro, Andante, etc.

    -- Enhanced classical metadata
    "Opus_Number"       TEXT,                   -- Op. 67, BWV 1006, K. 525, etc.
    "Catalog_Number"    TEXT,                   -- Kochel, BWV, D., Hob., etc.
    "Key"               TEXT,                   -- C minor, E-flat major, etc.
    "Composition_Year"  TEXT,                   -- Year work was composed

    -- Track-specific credits (overide album-level)
    "Track_Composer"    TEXT,                   -- If different from album
    "Track_Performer"   TEXT,                   -- Featured artist

    FOREIGN KEY("UPC") REFERENCES "albums"("UPC") ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Supporting Tables
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "sort_lookup" (
    "Artist"        TEXT PRIMARY KEY,
    "SortArtist"    TEXT NOT NULL
);

-- Normalized composer information
CREATE TABLE IF NOT EXISTS "composers" (
    "ComposerID"    INTEGER PRIMARY KEY AUTOINCREMENT,
    "Name"          TEXT NOT NULL UNIQUE,
    "SortName"      TEXT,
    "BirthYear"     INTEGER,
    "DeathYear"     INTEGER,
    "Period"        TEXT,               -- Baroque, Classical, Romantic, etc.
    "Nationality"   TEXT
);

-- Normalized musical works catalog
CREATE TABLE IF NOT EXISTS "works" (
    "WorkID"            INTEGER PRIMARY KEY AUTOINCREMENT,
    "ComposerID"        INTEGER,
    "Title"             TEXT NOT NULL,
    "Opus_Number"       TEXT,
    "Catalog_Number"    TEXT,
    "Key"               TEXT,
    "Composition_Year"  INTEGER,
    "Work_Type"         TEXT,           -- Symphony, Concerto, Sonata, etc.
    "Movement_Count"    INTEGER,        -- Expected number of movements

    FOREIGN KEY("ComposerID") REFERENCES "composers"("ComposerID")
);

-- Link tracks to formal works (optional, for detailed cataloging)
CREATE TABLE IF NOT EXISTS "track_works" (
    "TrackID"       INTEGER,
    "WorkID"        INTEGER,
    "MovementNum"   INTEGER,            -- Which movement this track represents

    PRIMARY KEY("TrackID", "WorkID"),
    FOREIGN KEY("TrackID") REFERENCES "tracks"("TrackID") ON DELETE CASCADE,
    FOREIGN KEY("WorkID") REFERENCES "works"("WorkID")
);

-- Many-to-many: albums can have multiple performers
CREATE TABLE IF NOT EXISTS "album_performers" (
    "UPC"           TEXT,
    "Performer"     TEXT NOT NULL,
    "Role"          TEXT,               -- Conductor, Soloist, Orchestra, etc.
    "Instrument"    TEXT,               -- For soloists

    PRIMARY KEY("UPC", "Performer", "Role"),
    FOREIGN KEY("UPC") REFERENCES "albums"("UPC") ON DELETE CASCADE
);

-- Track additional artists/credits
CREATE TABLE IF NOT EXISTS "track_credits" (
    "TrackID"       TEXT,
    "Artist"        TEXT NOT NULL,
    "Role"          TEXT,               -- Composer, performer, producer, etc.

    PRIMARY KEY("TrackID", "Artist", "Role"),
    FOREIGN KEY("TrackID") REFERENCES "tracks"("TrackID") ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Indexes for Performance
-- ----------------------------------------------------------------------------

CREATE INDEX IF NOT EXISTS "idx_albums_artist" ON "albums"("Artist");
CREATE INDEx IF NOT EXISTS "idx_albums_composer" ON "albums"("Composer");
CREATE INDEx IF NOT EXISTS "idx_albums_genre" ON "albums"("Genre");
CREATE INDEx IF NOT EXISTS "idx_albums_year" ON "albums"("Year");
CREATE INDEx IF NOT EXISTS "idx_albums_label" ON "albums"("Label");

CREATE INDEx IF NOT EXISTS "idx_tracks_upc" ON "tracks"("UPC");
CREATE INDEx IF NOT EXISTS "idx_tracks_parent_work" ON "tracks"("Parent_Work");
CREATE INDEx IF NOT EXISTS "idx_tracks_opus" ON "tracks"("Opus_Number");

CREATE INDEx IF NOT EXISTS "idx_works_composer" ON "works"("ComposerID");
CREATE INDEx IF NOT EXISTS "idx_works_type" ON "works"("Work_Type");

-- ----------------------------------------------------------------------------
-- Views for Common Queries
-- ----------------------------------------------------------------------------

-- Complete album view with all credits
CREATE VIEW IF NOT EXISTS "v_albums_complete" AS
SELECT
    a.*,
    COALESCE(a.Composer, a.Artist) as Display_Composer,
    COALESCE(a.Performer, a.Artist) as Display_Performer
FROM albums a;

-- Tracks with work information
CREATE VIEW IF NOT EXISTS "v_tracks_complete" AS
SELECT
    t.*,
    a.Artist as Album_Artist,
    a.Title as Album_Title,
    a.Composer as Album_Composer,
    COALESCE(t.Track_Composer, a.Composer, a.Artist) as Display_Composer,
    COALESCE(t.Track_Performer, a.Performer, a.Artist) as Display_Performer
FROM tracks t
JOIN albums a ON t.UPC = a.UPC;

-- Classical works catalog
CREATE VIEW IF NOT EXISTS "v_classical_works" AS
SELECT
    w.WorkID,
    w.Title as Work_Title,
    c.Name as Composer_Name,
    w.Opus_Number,
    w.Catalog_Number,
    w.Work_Type,
    w.Movement_Count,
    COUNT(DISTINCT t.TrackID) as Recordings_Count
FROM works w
JOIN composers c ON w.ComposerID = c.ComposerID
LEFT JOIN track_works tw ON w.WorkID = tw.WorkID
LEFT JOIN tracks t ON tw.TrackID = t.TrackID
GROUP BY w.WorkID;

-- Cover versions detection
CREATE VIEW IF NOT EXISTS "v_cover_versions" AS
SELECT
    t.TrackID,
    t.Track_Title,
    a.Artist as Peformer,
    a.Composer,
    a.Title as Album
FROM tracks t
JOIN albums a ON t.UPC = a.UPC
WHERE a.Composer IS NOT NULL
    AND a.Composer != ''
    AND a.Artist != a.Composer;

COMMIT