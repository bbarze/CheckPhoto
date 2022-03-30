# CheckPhoto

## This project help to keep organized photos on PC. 

### Do you have any photos that may be a duplicate of photos already in your library? This project will find duplicates and handle them in the best way.

For each photo in the folder considered (folder A), all the photos (if present) with the same name are recovered from the library (folder B).

For all photos with the same name the following cases can occur:
1. The photos are IDENTICAL: then the photo from folder A is moved to the trash
2. The photos are similar:<br />
   2.1 The photo in folder A has a lower resolution than the photo in folder B: the photo with lower resolution is moved to the trash<br />
   2.2 The photo in folder A has a higher resolution than the photo in folder B: the lower resolution photo is moved to the trash and the higher resolution photo is put in its place
3. The two photos are not similar: no operation is performed
