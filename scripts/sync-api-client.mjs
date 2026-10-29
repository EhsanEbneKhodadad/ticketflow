import { copyFile, lstat, mkdir, readdir, stat } from "node:fs/promises";
import { join } from "node:path";
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
  await mkdir(installed, { recursive: true });
  await copyFile(join(source, "package.json"), join(installed, "package.json"));
  const syncDirectory = async (relativePath) => {
    const from = join(source, relativePath);
    const to = join(installed, relativePath);
    await mkdir(to, { recursive: true });
    for (const entry of await readdir(from, { withFileTypes: true })) {
      const relativeEntry = join(relativePath, entry.name);
      if (entry.isDirectory()) {
        await syncDirectory(relativeEntry);
        continue;
      }
      const sourceFile = join(source, relativeEntry);
      const installedFile = join(installed, relativeEntry);
      const sourceStat = await stat(sourceFile);
      const installedStat = await stat(installedFile).catch(() => undefined);
      if (
        !installedStat ||
        sourceStat.dev !== installedStat.dev ||
        sourceStat.ino !== installedStat.ino
      ) {
        await copyFile(sourceFile, installedFile);
      }
    }
  };
  await syncDirectory("src");
}
console.log("Installed API contract synchronized.");
