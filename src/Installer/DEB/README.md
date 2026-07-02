1. choissez architecture : amd64, arm64, armhf
2. Renomez le dossier en mettant l'architecture à la fin. EX : SaveMail-x64, SaveMail-arm64, SaveMail-armhf.
3. Ajoutez l'executable "SaveMail" dans le dossier SaveMail-arch/usr/bin/.
4. Editez le fichier /DEBIAN/control -> modifier avec l'architecture choisi.

5. modifier les droits : 
chmod 755 SaveMail-amd64/DEBIAN
chmod 644 SaveMail-amd64/DEBIAN/control
chmod 755 SaveMail-amd64
chmod 755 SaveMail-amd64/usr/bin/SaveMail

5. Compilez : dpkg-deb --build SaveMail-amd64
