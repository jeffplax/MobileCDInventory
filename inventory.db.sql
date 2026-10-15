BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "albums" (
	"UPC"	TEXT,
	"Artist"	TEXT,
	"Title"	TEXT,
	"Year"	TEXT,
	"Format"	TEXT,
	"Label"	TEXT,
	"Genre"	TEXT,
	"Styles"	TEXT,
	"SortArtist"	TEXT,
	PRIMARY KEY("UPC")
);
CREATE TABLE IF NOT EXISTS "sort_lookup" (
	"Artist"	TEXT,
	"SortArtist"	TEXT
);
CREATE TABLE IF NOT EXISTS "tracks" (
	"TrackID"	INTEGER,
	"UPC"	TEXT,
	"Position"	TEXT,
	"Parent_Work"	TEXT,
	"Track_Title"	TEXT,
	"Duration"	TEXT,
	PRIMARY KEY("TrackID" AUTOINCREMENT),
	FOREIGN KEY("UPC") REFERENCES "albums"("UPC") ON DELETE CASCADE
);
COMMIT;
