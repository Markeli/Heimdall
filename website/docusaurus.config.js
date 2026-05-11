// @ts-check
// Docusaurus 3 configuration for the Heimdall documentation site.
// Source markdown lives OUTSIDE this directory — see `presets.docs.path` below.

import { themes as prismThemes } from "prism-react-renderer";

/** @type {import('@docusaurus/types').Config} */
const config = {
	title: "Heimdall",
	tagline: "Internal proxy for public package repositories",
	favicon: "img/favicon.svg",

	url: "https://markeli.github.io",
	baseUrl: "/Heimdall/",

	organizationName: "Markeli",
	projectName: "Heimdall",

	onBrokenLinks: "throw",
	onBrokenAnchors: "warn",

	i18n: {
		defaultLocale: "en",
		locales: ["en"],
	},

	presets: [
		[
			"classic",
			/** @type {import('@docusaurus/preset-classic').Options} */
			({
				docs: {
					// Load source markdown from the repo-root `/docs` folder so that it stays
					// browsable directly on GitHub.
					path: "../docs",
					routeBasePath: "/",
					sidebarPath: "./sidebars.js",
					editUrl: "https://github.com/Markeli/Heimdall/edit/main/",
				},
				blog: false,
				theme: {
					customCss: "./src/css/custom.css",
				},
			}),
		],
	],

	themes: [
		[
			"@easyops-cn/docusaurus-search-local",
			/** @type {import('@easyops-cn/docusaurus-search-local').PluginOptions} */
			({
				hashed: true,
				language: ["en"],
				indexBlog: false,
				docsDir: "../docs",
				docsRouteBasePath: "/",
				highlightSearchTermsOnTargetPage: true,
			}),
		],
	],

	themeConfig:
		/** @type {import('@docusaurus/preset-classic').ThemeConfig} */
		({
			colorMode: {
				defaultMode: "light",
				respectPrefersColorScheme: true,
			},
			navbar: {
				title: "Heimdall",
				logo: {
					alt: "Heimdall",
					src: "img/logo.svg",
				},
				items: [
					{
						type: "docSidebar",
						sidebarId: "docs",
						position: "left",
						label: "Docs",
					},
					{
						href: "https://github.com/Markeli/Heimdall",
						label: "GitHub",
						position: "right",
					},
				],
			},
			footer: {
				style: "dark",
				links: [
					{
						title: "Docs",
						items: [
							{ label: "Introduction", to: "/" },
							{ label: "Quick start", to: "/getting-started/quick-start" },
							{ label: "Configuration", to: "/configuration/overview" },
						],
					},
					{
						title: "Project",
						items: [
							{
								label: "Repository",
								href: "https://github.com/Markeli/Heimdall",
							},
							{
								label: "Issues",
								href: "https://github.com/Markeli/Heimdall/issues",
							},
							{
								label: "Releases",
								href: "https://github.com/Markeli/Heimdall/releases",
							},
						],
					},
					{
						title: "Contributing",
						items: [
							{
								label: "CONTRIBUTING.md",
								href: "https://github.com/Markeli/Heimdall/blob/main/CONTRIBUTING.md",
							},
							{
								label: "AGENTS.md",
								href: "https://github.com/Markeli/Heimdall/blob/main/AGENTS.md",
							},
							{
								label: "CHANGELOG.md",
								href: "https://github.com/Markeli/Heimdall/blob/main/CHANGELOG.md",
							},
						],
					},
				],
				copyright: `Heimdall — released under the project license. Built with Docusaurus.`,
			},
			prism: {
				theme: prismThemes.github,
				darkTheme: prismThemes.oneDark,
				additionalLanguages: ["csharp", "yaml", "bash", "docker", "json", "powershell"],
			},
		}),
};

export default config;
