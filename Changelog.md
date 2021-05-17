# Changelog
All notable changes to this package will be documented in this file.
The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2021-05-13
- added: ability to create UploadConfigs that can specify multiple folders/packages for store upload
- added: prevent packages that start with "com.unity." from being uploaded
- added: prevent packages in the Library from being uploaded (only local/embedded packages should be)
- added: sample UploadConfig, import via PackMan samples.

## [0.3.0] - 2021-05-03
- embedded Harmony plugin to simplify dependencies
- fixed: DLLs were not exported in some cases because Unity treats them as DefaultAssets, same as folders

## [0.2.0] - 2021-04-28
- initial OpenUPM release
- Readme adjustments

## [0.1.0-exp] - 2021-04-10
- initial package version
- supports `Asset Store Tools 5.0`
- supports `Assets/Export Package`
- tested against Unity 2018.4, 2019.4, 2020.3