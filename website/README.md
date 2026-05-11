# Heimdall documentation site

This is the [Docusaurus 3](https://docusaurus.io/) project that publishes the
Heimdall docs to GitHub Pages.

## Prerequisites

- Node.js 20 LTS (see [`.nvmrc`](./.nvmrc) — `nvm use` picks it up).
- A working clone of the Heimdall repository. Source markdown lives in
  [`../docs`](../docs), not in `./docs`. The Docusaurus `path` option in
  [`docusaurus.config.js`](./docusaurus.config.js) points there explicitly.

## Local preview

```sh
cd website
npm ci
npm start
```

The dev server runs at <http://localhost:3000/Heimdall/>. Hot reload reflects
changes to both `../docs/**/*.md` and the Docusaurus config.

## Production build

```sh
npm run build
npm run serve   # optional — serves ./build at http://localhost:3000/Heimdall/
```

This is the command CI runs in [`../.github/workflows/docs.yml`](../.github/workflows/docs.yml).
`onBrokenLinks: "throw"` makes the build fail on any unresolved internal link.

## Adding a page

1. Add the `.md` file under `../docs/<category>/` (not `./docs/`).
2. Reference it in [`sidebars.js`](./sidebars.js) so it appears in the sidebar.
3. Run `npm start` to verify navigation and search.

Front matter is optional. When absent, Docusaurus derives the page title from
the first `# Heading`.

## Asset paths

`baseUrl` is `/Heimdall/`, so the site is served from a sub-path. Keep static
assets in [`./static/`](./static) and reference them with absolute paths:

```md
![diagram](/img/diagram.png)
```

Docusaurus rewrites these at build time. Relative `../static/...` paths in
markdown do **not** work.

## Search

Local search is provided by `@easyops-cn/docusaurus-search-local`. The search
index is built at `npm run build` time — no runtime service, no external
account. Configuration lives in `docusaurus.config.js` under `themes`.
