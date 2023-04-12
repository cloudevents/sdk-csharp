#!/bin/bash
# Copyright 2021 Cloud Native Foundation.
# Licensed under the Apache 2.0 license.
# See LICENSE file in the project root for full license information.

set -e
PROTOBUF_VERSION=22.0

# Generates the classes for the protobuf event format

case "$OSTYPE" in
  linux*)
    PROTOBUF_PLATFORM=linux-x86_64
    PROTOC=tmp/bin/protoc
    ;;
  win* | msys* | cygwin*)
    PROTOBUF_PLATFORM=win64
    PROTOC=tmp/bin/protoc.exe
    ;;
  darwin*)
    PROTOBUF_PLATFORM=osx-x86_64
    PROTOC=tmp/bin/protoc
    ;;
  *)
    echo "Unknown OSTYPE: $OSTYPE"
    exit 1
esac

# Clean up previous generation results
rm -f src/CloudNative.CloudEvents.Protobuf/*.g.cs
rm -f test/CloudNative.CloudEvents.UnitTests/Protobuf/*.g.cs

rm -rf tmp
mkdir tmp
cd tmp

echo "- Downloading protobuf@$PROTOBUF_VERSION"
curl -sSL \
  https://github.com/protocolbuffers/protobuf/releases/download/v$PROTOBUF_VERSION/protoc-$PROTOBUF_VERSION-$PROTOBUF_PLATFORM.zip \
  --output protobuf.zip
unzip -q protobuf.zip

echo "- Downloading schema"
# TODO: Use the 1.0.2 branch when it exists.
mkdir cloudevents
curl -sSL https://raw.githubusercontent.com/cloudevents/spec/main/cloudevents/formats/cloudevents.proto -o cloudevents/cloudevents.proto

cd ..

# Schema proto
$PROTOC \
  -I tmp/include \
  -I tmp/cloudevents \
  --csharp_out=src/CloudNative.CloudEvents.Protobuf \
  --csharp_opt=file_extension=.g.cs \
  tmp/cloudevents/cloudevents.proto

# Test protos
$PROTOC \
  -I tmp/include \
  -I test/CloudNative.CloudEvents.UnitTests/Protobuf \
  --csharp_out=test/CloudNative.CloudEvents.UnitTests/Protobuf \
  --csharp_opt=file_extension=.g.cs \
  test/CloudNative.CloudEvents.UnitTests/Protobuf/*.proto

# Conformance test protos
$PROTOC \
  -I tmp/include \
  -I tmp/cloudevents \
  -I conformance/format/protobuf \
  --csharp_out=test/CloudNative.CloudEvents.UnitTests/Protobuf \
  --csharp_opt=file_extension=.g.cs \
  conformance/format/protobuf/*.proto

echo "Generated code."
rm -rf tmp
