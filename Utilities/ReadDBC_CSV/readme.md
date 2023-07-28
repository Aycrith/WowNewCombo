# ReadDBC_CSV

**som_game_build** = 1.14.3.49821
**tbc_game_build** = 2.5.4.44833
**wrath_game_build** = 3.4.2.50375

# ReadDBC_CSV_Consumables - What it does
* It generates the available Food and Water consumables list based on the given DBC files.

## ReadDBC_CSV_Consumables - Required DBC files
* data/spell.csv
https://wago.tools/db2/spell/csv?&build=3.4.2.50375

* data/itemeffect.csv
https://wago.tools/db2/itemeffect/csv?&build=3.4.2.50375

## ReadDBC_CSV_Consumables - Produces
* data/foods.json
* data/waters.json


---
# ReadDBC_CSV_Spell - What it does
* It generates the available spell(id, name, level) list based on the given DBC file.

## ReadDBC_CSV_Spell - Required DBC files
* data/spellname.csv
https://wago.tools/db2/spellname/csv?&build=3.4.2.50375

* data/spelllevels.csv
https://wago.tools/db2/spelllevels/csv?&build=3.4.2.50375

## ReadDBC_CSV_Spell - Produces
* data/spells.json


---
# ReadDBC_CSV_Talents - What it does
* It generates the available talents based on the given DBC file.

## ReadDBC_CSV_Talents - Required DBC files
* data/talenttab.csv
https://wago.tools/db2/talenttab/csv?&build=3.4.2.50375

* data/talent.csv
https://wago.tools/db2/talent/csv?&build=3.4.2.50375

## ReadDBC_CSV_Talents - Produces
* data/talent.json
* data/talenttab.json


---
# ReadDBC_CSV_WorldMapArea - What it does
* It generates the WorldMapArea.json list based on the given DBC files.

## ReadDBC_CSV_WorldMapArea - Required DBC files
* data/uimap.csv
https://wago.tools/db2/uimap/csv?&build=3.4.2.50375

* data/uimapassignment.csv
https://wago.tools/db2/uimapassignment/csv?&build=3.4.2.50375

* data/map.csv
https://wago.tools/db2/map/csv?&build=3.4.2.50375

## ReadDBC_CSV_WorldMapArea - Produces
* data/WorldMapArea.json