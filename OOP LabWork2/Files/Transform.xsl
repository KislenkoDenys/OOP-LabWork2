<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:output method="html" indent="yes" encoding="UTF-8"/>

	<xsl:template match="/ScientistsResults">
		<html>
			<head>
				<title>Результати Пошуку</title>
				<style>
					body {
					font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
					margin: 0;
					padding: 10px;
					background-color: #262638;
					color: #CDD6F4;
					}

					h2 {
					color: #89B4FA;
					border-bottom: 2px solid #45475A;
					padding-bottom: 10px;
					}
					table {
					width: 100%;
					border-collapse: collapse;
					margin-top: 10px;
					background-color: #1E1E2E;
					border-radius: 10px;
					overflow: hidden;
					box-shadow: 0 4px 6px rgba(0,0,0,0.3);
					}
					th {
					background-color: #313244;
					color: #CBA6F7;
					padding: 15px;
					text-align: left;
					font-weight: bold;
					text-transform: uppercase;
					font-size: 0.85em;
					letter-spacing: 1px;
					border-bottom: 2px solid #45475A;
					}
					td {
					padding: 12px 15px;
					border-bottom: 1px solid #45475A;
					}
					tr:hover {
					background-color: #313244;
					transition: background-color 0.2s;
					}
					tr:last-child td {
					border-bottom: none;
					}
					ul {
					margin: 0;
					padding-left: 20px;
					color: #A6E3A1;
					}
					li {
					margin-bottom: 4px;
					}

					.degree-tag {
					background-color: #45475A;
					color: #F38BA8;
					padding: 2px 8px;
					border-radius: 4px;
					font-size: 0.9em;
					font-weight: bold;
					}
				</style>
			</head>
			<body>
				<xsl:if test="count(Scientist) = 0">
					<div style="text-align:center; padding: 20px; color: #F38BA8;">
						<h3>Записів не знайдено</h3>
					</div>
				</xsl:if>

				<xsl:if test="count(Scientist) > 0">
					<table>
						<thead>
							<tr>
								<th>П.І.П.</th>
								<th>Факультет / Кафедра</th>
								<th>Науковий Ступінь</th>
								<th>Вчені Звання</th>
							</tr>
						</thead>
						<tbody>
							<xsl:apply-templates select="Scientist"/>
						</tbody>
					</table>
				</xsl:if>
			</body>
		</html>
	</xsl:template>

	<xsl:template match="Scientist">
		<tr>
			<td style="font-weight: bold; color: #89B4FA;">
				<xsl:value-of select="FullName"/>
			</td>
			<td>
				<div style="margin-bottom: 5px;">
					🏛 <xsl:value-of select="Faculty"/>
				</div>
				<div style="font-size: 0.9em; color: #A6ADC8;">
					📂 <xsl:value-of select="Department"/>
				</div>
			</td>
			<td>
				<span class="degree-tag">
					<xsl:value-of select="Degree/@type"/>
				</span>
				<br/>
				<span style="font-size: 0.9em; font-style: italic; margin-top: 4px; display:inline-block;">
					<xsl:value-of select="Degree"/>
				</span>
			</td>
			<td>
				<ul>
					<xsl:for-each select="Ranks/Rank">
						<li>
							<xsl:value-of select="@title"/>
							<span style="color: #6C7086; font-size: 0.85em;">
								(<xsl:value-of select="@date"/>)
							</span>
						</li>
					</xsl:for-each>
				</ul>
			</td>
		</tr>
	</xsl:template>

</xsl:stylesheet>