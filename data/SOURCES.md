# Source inventory and attribution

- HeroesToolChest/heroes-data2: canonical game structures and twelve locale files;
  MIT repository license, copyright Kevin Oliva (see bundled licenses).
- HeroesToolChest/heroes-images: image inventory; MIT repository license, copyright
  Kevin Oliva. Its disclaimer states game images belong to Blizzard Entertainment.
  Preserve Blizzard attribution: Heroes of the Storm and related game assets are
  Blizzard Entertainment property. This project claims no ownership of game assets.
- heroespatchnotes/heroes-patch-data: MIT historical release metadata and source links.
- BlueTracker and linked Blizzard news/forum posts: existing official-note discovery
  path; retain source URLs and distinguish official content from its mirror.
- Fixed Internet Archive captures of the same official Blizzard posts may recover
  broken historical URLs. They are not new authored community notes.
- Archived HeroesPatchNotes/community reports: retain metadata/source links pending
  recorded reuse approval. The registry's allowContent flag must not be enabled
  without a documented source/attribution decision. Missing notes are permitted.
- Nexus Patch Notes: NO runtime ingestion, fixture or redistributed text/images.
  It was used only during investigation to find underlying source URLs; its
  All Rights Reserved restriction in the project policy remains in effect.

All twelve game-data locales are required. This does not imply twelve official
translations of every patch article. The current official-note discovery adapter
retrieves US English; articles in other locales require a verified discovery
adapter rather than machine translations labeled as official.

Repository software licenses do not transfer ownership of Blizzard's assets or
third-party prose. Distribution readiness requires reviewing this inventory and
any newly enabled historical source. Runtime code has no broad web scraper or
arbitrary-source download option. See manifest.json for exact pinned inputs.
