// @ts-check
// Manual sidebar definition; keeps page order deterministic regardless of file system order.

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
	docs: [
		"intro",
		{
			type: "category",
			label: "Getting started",
			collapsed: false,
			items: [
				"getting-started/installation",
				"getting-started/quick-start",
			],
		},
		{
			type: "category",
			label: "Configuration",
			collapsed: false,
			items: [
				"configuration/overview",
				"configuration/server",
				"configuration/cache",
				"configuration/feeds",
				"configuration/logging",
			],
		},
		{
			type: "category",
			label: "Filtering rules",
			collapsed: false,
			items: [
				"rules/overview",
				"rules/min-age-days",
				"rules/allow-deny",
			],
		},
		{
			type: "category",
			label: "API",
			collapsed: true,
			items: [
				"api/nuget-v3",
				"api/health-and-metrics",
			],
		},
		{
			type: "category",
			label: "Operations",
			collapsed: true,
			items: [
				"operations/deployment",
				"operations/monitoring",
				"operations/troubleshooting",
			],
		},
		{
			type: "category",
			label: "Architecture",
			collapsed: true,
			items: [
				"architecture/overview",
				"architecture/caching",
				"architecture/filtering-pipeline",
			],
		},
		{
			type: "category",
			label: "Development",
			collapsed: true,
			items: [
				"development/building",
				"development/testing",
			],
		},
	],
};

export default sidebars;
