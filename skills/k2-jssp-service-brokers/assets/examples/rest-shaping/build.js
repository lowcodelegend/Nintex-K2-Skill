const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = __dirname;
const source = path.join(root, "src", "index.js");
const targetDir = path.join(root, "dist");
const target = path.join(targetDir, "k2skills-rest-shaping.jssp");
const code = fs.readFileSync(source, "utf8");

new vm.Script(code, { filename: source });
fs.mkdirSync(targetDir, { recursive: true });
fs.writeFileSync(target, code.replace(/\r?\n/g, "\r\n"), "utf8");
console.log(`Built ${target}`);
