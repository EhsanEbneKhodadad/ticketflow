import { copyFile, lstat, mkdir } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const source = join(root, "packages", "api-client");
const installed = join(root, "node_modules", "@ticketflow", "api-client");

// Hoisted installs use a physical copy on Windows. If a developer switches
// to workspace symlinks, the source already is the installed contract.
const info = await lstat(installed).catch((error) => {
  if (error.code !== "ENOENT") throw error;
  return undefined;
});
if (!info?.isSymbolicLink()) {
  for (const file of ["package.json", "src/index.ts", "src/schema.d.ts"]) {
    const destination = join(installed, file);
    await mkdir(dirname(destination), { recursive: true });
    await copyFile(join(source, file), destination);
  }
}
console.log("Installed API contract synchronized.");
