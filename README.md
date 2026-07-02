# SaveMail

Un utilitaire de bureau moderne, minimaliste et 100 % hors ligne permettant de convertir facilement vos sauvegardes d'emails (`.eml`, `.msg`) en documents PDF. 

L'application intègre intelligemment le corps de l'email et les pièces jointes compatibles dans un fichier PDF unique. Les pièces jointes non prises en charge pour la fusion (vidéos, formats propriétaires complexes) sont automatiquement extraites et regroupées dans une archive ZIP jointe.

## Fonctionnalités

* **Importation intuitive :** Glissez-déposez (Drag & Drop) vos fichiers d'emails directement dans l'interface, ou utilisez l'explorateur de fichiers de votre système.
* **Support multilingue :** L'interface est disponible nativement en plusieurs langues (anglais, français, allemand, italien).
* **Gestion de file d'attente :** Traitez plusieurs emails en lot. Suivez l'avancement global et individuel grâce à une barre de progression claire.
* **Gestion intelligente des pièces jointes :**
  * Les fichiers visuels ou simples (images, textes, autres PDF) sont convertis et ajoutés à la suite de l'email dans le PDF final.
  * Les fichiers complexes (vidéos, documents Office, etc.) sont sauvegardés en toute sécurité dans une archive `.zip` accompagnant le PDF.
* **Options d'exportation personnalisables :** Choisissez librement le répertoire de sortie et votre mode d'export (Tout dans le PDF vs. PDF + Archive ZIP).
* **Visualisation rapide :** Un bouton d'action permet d'ouvrir immédiatement le PDF généré dans votre navigateur web ou votre lecteur par défaut.
* **Zéro dépendance (Portable) :** L'application est fournie sous forme d'un exécutable unique et autonome. Aucune installation de framework, de base de données ou de Microsoft Office n'est requise sur la machine.
* **Multiplateforme :** Fonctionne de manière native sur Windows et Linux.

## Technologies Utilisées

* **Framework UI :** [Avalonia UI](https://avaloniaui.net/) (C# / .NET) - Pour une interface moderne, fluide et multiplateforme.
...

## Confidentialité et Limitations

* **100 % Hors Ligne :** Le respect de la vie privée est au cœur de cet outil. Aucune donnée n'est envoyée sur internet. L'intégralité de l'extraction et de la conversion est effectuée localement sur le processeur de votre machine.
* **Rendu des emails :** La conversion du corps du message en PDF vise à être la plus fidèle possible au format d'origine. Toutefois, certains styles CSS extrêmement complexes présents dans des newsletters commerciales peuvent être simplifiés lors du rendu.
