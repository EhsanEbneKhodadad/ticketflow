import { TextDecoder, TextEncoder } from "node:util";
import * as webStreams from "node:stream/web";

Object.assign(globalThis, {
  ...webStreams,
  TextDecoder,
  TextEncoder,
  structuredClone: globalThis.structuredClone ?? ((value: unknown) => value),
});
