# Changelog

## [1.3.0] - September 9, 2022

* Fixed stack overflow exception in newer versions of unity. Reported by BlackManateeon via Unity Asset store review using Unity 2021.3
* Added scroll support and maximum component length. This fixes issue where the toolbar becomes un-usable when readme is beyond a certain length.
* Refactored overly complex ReadmeEditor.cs into ReadmeEditor, ReadmeTextEditor, and ReadmeTextArea for QOL
* Fixed bug in serialization where ObjectFields references were changing order. 
* Added free version to enable distribution with any assets and ease of use. 

## [1.2.0] - Jun 29, 2021

* Bug fixes on serializing object field references. 
* Added support for PDF exporting. 
* Added support for free version, coming soon.
* Added HtmlAgilityPack integration for better tag editing.

## [1.1.0] - Sep 3, 2019

* Basic text editing.
* Toolbar with rich text options.