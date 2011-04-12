﻿-- ----------------------------
--  Table structure for "nko_1TI_6";
-- ----------------------------
DROP TABLE IF EXISTS "nko_1TI_6";
CREATE TABLE "nko_1TI_6" (
	"id" int4 DEFAULT NULL,
	"version" char(6) DEFAULT NULL,
	"book" char(7) DEFAULT NULL,
	"verse" float4 DEFAULT NULL,
	"unformatted" text DEFAULT NULL,
	"idxfti" tsvector DEFAULT NULL,
	"basichtml" text DEFAULT NULL
)
WITH (OIDS=FALSE);
ALTER TABLE "nko_1TI_6" OWNER TO "root";

-- ----------------------------
--  Records of "nko_1TI_6"
-- ----------------------------
BEGIN;
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.001', 'Ilehián ánɩ́ nkpábiá bʋbʋ ɔpasua ámʋtɔ bɛ́hɩɛ bú amʋ́ awíe, mɛ́nɩ ɔhaa mɔ́ɔkpɔɩ́ Bulu mʋa anɩ atosunáhɛ́ amʋ. ', '', '&lt;span class="verse" id="1TI_6_1"&gt;&lt;strong class="verseno"&gt;1&lt;/strong&gt;&amp;nbsp;Ilehián ánɩ́ nkpábiá bʋbʋ ɔpasua ámʋtɔ bɛ́hɩɛ bú amʋ́ awíe, mɛ́nɩ ɔhaa mɔ́ɔkpɔɩ́ Bulu mʋa anɩ atosunáhɛ́ amʋ. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.002', 'Ilehián ánɩ́ amʋ́á amʋ́ awíe bʋbʋ ɔpasua ámʋtɔ bóbu amʋ́ awíe amʋ dʋn. Séi kʋ́ráá ilehián ánɩ́ bóbu amʋ́ dʋn nɩ, tsúfɛ́ ahógyipʋ bʋgyi, bʋtɔdwɛ́ amʋ́ ɛ́.
Ɩ́nɩ aná gyí tɔ́á fʋsúna amʋ́, afʋwa amʋ́ atɛtɔ-ɔnlɩn abʋbwɛ nɩ́. ', '', '&lt;span class="verse" id="1TI_6_2"&gt;&lt;strong class="verseno"&gt;2&lt;/strong&gt;&amp;nbsp;Ilehián ánɩ́ amʋ́á amʋ́ awíe bʋbʋ ɔpasua ámʋtɔ bóbu amʋ́ awíe amʋ dʋn. Séi kʋ́ráá ilehián ánɩ́ bóbu amʋ́ dʋn nɩ, tsúfɛ́ ahógyipʋ bʋgyi, bʋtɔdwɛ́ amʋ́ ɛ́.
&lt;h2&gt;Afunu Atosunáhɛ́ Pʋ́ Atonyahɛ́ Onutó&lt;/h2&gt;Ɩ́nɩ aná gyí tɔ́á fʋsúna amʋ́, afʋwa amʋ́ atɛtɔ-ɔnlɩn abʋbwɛ nɩ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.003', 'Nɩ́ ɔkʋ lósuná atɔ laláhɛ kʋ, mʋ́a anɩ Wíe Yesu Kristo asʋn blɩ́hɛ́ pʋ́ Bulusúm atosunáhɛ́ amʋ ɩmɛná á, ', '', '&lt;span class="verse" id="1TI_6_3"&gt;&lt;strong class="verseno"&gt;3&lt;/strong&gt;&amp;nbsp;Nɩ́ ɔkʋ lósuná atɔ laláhɛ kʋ, mʋ́a anɩ Wíe Yesu Kristo asʋn blɩ́hɛ́ pʋ́ Bulusúm atosunáhɛ́ amʋ ɩmɛná á, &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.004', 'idesuná ánɩ́ ɔhá ámʋ dɛ́ ɩwɩ tsu. Omeyín tɔtɔ, nwɛ́ɛnkpangyí labʋ́lá mʋtɔ. Mʋ́á ɩtɛdálɩ tsu mʋ́tɔ́ gyí ɔnsɩ́pɛ, ɩtrɛ́, isia pʋ́ agywɩɩn laláhɛ. ', '', '&lt;span class="verse" id="1TI_6_4"&gt;&lt;strong class="verseno"&gt;4&lt;/strong&gt;&amp;nbsp;idesuná ánɩ́ ɔhá ámʋ dɛ́ ɩwɩ tsu. Omeyín tɔtɔ, nwɛ́ɛnkpangyí labʋ́lá mʋtɔ. Mʋ́á ɩtɛdálɩ tsu mʋ́tɔ́ gyí ɔnsɩ́pɛ, ɩtrɛ́, isia pʋ́ agywɩɩn laláhɛ. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.005', 'Asʋ́n ánɩ́ imedéhián ɩwɩ nwɛ́ɛn ahá ánfɩ odu bʋtɛnátɩ́ gyí. Amʋ́ agywɩɩn lawú, bʋtamablɩ́ ɔnɔkwalɩ. Amʋ́ odu bʋtosúsu ánɩ́ Bulusúm igyi ɔkpa kʋá fɔ́tsʋn mʋ́sʋ́ nyá ɩwɩ.
', '', '&lt;span class="verse" id="1TI_6_5"&gt;&lt;strong class="verseno"&gt;5&lt;/strong&gt;&amp;nbsp;Asʋ́n ánɩ́ imedéhián ɩwɩ nwɛ́ɛn ahá ánfɩ odu bʋtɛnátɩ́ gyí. Amʋ́ agywɩɩn lawú, bʋtamablɩ́ ɔnɔkwalɩ. Amʋ́ odu bʋtosúsu ánɩ́ Bulusúm igyi ɔkpa kʋá fɔ́tsʋn mʋ́sʋ́ nyá ɩwɩ.&lt;p&gt; &lt;/p&gt;&lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.006', 'Labi kpɔnkpɔntɩ bʋ Bulusúmtɔ́ nɩ́ tɔ́á anɩbʋ yaɩ́ legyi anɩ ansɩ́. ', '', '&lt;span class="verse" id="1TI_6_6"&gt;&lt;strong class="verseno"&gt;6&lt;/strong&gt;&amp;nbsp;Labi kpɔnkpɔntɩ bʋ Bulusúmtɔ́ nɩ́ tɔ́á anɩbʋ yaɩ́ legyi anɩ ansɩ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.007', 'Tsúfɛ́ anɩmɔ́pʋ tɔtɔ ba ɔyɩ́tɔ́, anɩmɛ́ɛtalɩ́ pʋ́ tɔtɔɔtɔ ɛ́ nátɩ́. ', '', '&lt;span class="verse" id="1TI_6_7"&gt;&lt;strong class="verseno"&gt;7&lt;/strong&gt;&amp;nbsp;Tsúfɛ́ anɩmɔ́pʋ tɔtɔ ba ɔyɩ́tɔ́, anɩmɛ́ɛtalɩ́ pʋ́ tɔtɔɔtɔ ɛ́ nátɩ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.008', 'Mʋ́ sʋ nɩ́ anɩbʋ atogyihɛ pʋ́ atɔ dɩdáhɛ́ a, mlɩha mʋ́ kpán igyi anɩ ansɩ́. ', '', '&lt;span class="verse" id="1TI_6_8"&gt;&lt;strong class="verseno"&gt;8&lt;/strong&gt;&amp;nbsp;Mʋ́ sʋ nɩ́ anɩbʋ atogyihɛ pʋ́ atɔ dɩdáhɛ́ a, mlɩha mʋ́ kpán igyi anɩ ansɩ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.009', 'Ahá ánɩ́ bʋtekleá bʋnyáa atɔ́ á, ɩbwɛ́bwɛ́ɛ́bwɛ́ bʋtɛkpá wíé ɩsɔ́kɩ pʋ́ ayibitɔ. Amʋ́ mimlála tɛkpá amʋ́ yówie ɩwɩɔsɩn mʋ́a lalahɛtɔ, hɩ́ amʋ́. ', '', '&lt;span class="verse" id="1TI_6_9"&gt;&lt;strong class="verseno"&gt;9&lt;/strong&gt;&amp;nbsp;Ahá ánɩ́ bʋtekleá bʋnyáa atɔ́ á, ɩbwɛ́bwɛ́ɛ́bwɛ́ bʋtɛkpá wíé ɩsɔ́kɩ pʋ́ ayibitɔ. Amʋ́ mimlála tɛkpá amʋ́ yówie ɩwɩɔsɩn mʋ́a lalahɛtɔ, hɩ́ amʋ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.010', 'Tsúfɛ́ kɔ́badwɛ tɔ́pʋ́ lalahɛ kugyíkʋ ba. Mʋ́ ɔma akʋ bagya alɩɩ, bafwɩ́ dalɩ hógyi amʋtɔ, ɩlahá amʋ́ bʋdɛ awɩrɛhɔ tsɔtsɔɔtsɔ gyí nɩ.
', '', '&lt;span class="verse" id="1TI_6_10"&gt;&lt;strong class="verseno"&gt;10&lt;/strong&gt;&amp;nbsp;Tsúfɛ́ kɔ́badwɛ tɔ́pʋ́ lalahɛ kugyíkʋ ba. Mʋ́ ɔma akʋ bagya alɩɩ, bafwɩ́ dalɩ hógyi amʋtɔ, ɩlahá amʋ́ bʋdɛ awɩrɛhɔ tsɔtsɔɔtsɔ gyí nɩ.
&lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.011', 'Timoteo, fʋ mʋ́ á, Bulu ɔha fʋgyi. Mʋ́ sʋ ha ɩ́nɩ aná ɩwa ɩfɔ́ fʋ́ wá. Hɩɛ fʋ́ ɩwɩ, afʋbwɛ tɔ́á ɩda ɔkpa. Bɔ mbɔ́dɩ́ afʋha yilébwɛ, Bulusúm, hógyi, ɔdwɛ, klʋnya pʋ́ ɩwɩasɩbá itsía fʋ́tɔ́. ', '', '&lt;h2&gt;Paulo Asʋ́n Tráhɛ&lt;/h2&gt;&lt;span class="verse" id="1TI_6_11"&gt;&lt;strong class="verseno"&gt;11&lt;/strong&gt;&amp;nbsp;Timoteo, fʋ mʋ́ á, Bulu ɔha fʋgyi. Mʋ́ sʋ ha ɩ́nɩ aná ɩwa ɩfɔ́ fʋ́ wá. Hɩɛ fʋ́ ɩwɩ, afʋbwɛ tɔ́á ɩda ɔkpa. Bɔ mbɔ́dɩ́ afʋha yilébwɛ, Bulusúm, hógyi, ɔdwɛ, klʋnya pʋ́ ɩwɩasɩbá itsía fʋ́tɔ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.012', 'Kɔ hógyi ɩkɔ́ wankláán ámʋ. Fʋ́ ɩbɩ bɛ́da nkpa ámʋ́ʋ́ ɩtamatá, Bulu obékie anɩ mʋ́ ɔma amʋ. Nkpa ánfɩ sʋ́ Bulu lɛ́tɩ fʋ́ nɩ́. Mʋ́ sʋ fobwií ɔnɔ́ ahá tsɔtsɔɔtsɔ ansɩ́tɔ́, gyi fʋ́ hógyi ɩwɩ adánsɩɛ nɩ́. ', '', '&lt;span class="verse" id="1TI_6_12"&gt;&lt;strong class="verseno"&gt;12&lt;/strong&gt;&amp;nbsp;Kɔ hógyi ɩkɔ́ wankláán ámʋ. Fʋ́ ɩbɩ bɛ́da nkpa ámʋ́ʋ́ ɩtamatá, Bulu obékie anɩ mʋ́ ɔma amʋ. Nkpa ánfɩ sʋ́ Bulu lɛ́tɩ fʋ́ nɩ́. Mʋ́ sʋ fobwií ɔnɔ́ ahá tsɔtsɔɔtsɔ ansɩ́tɔ́, gyi fʋ́ hógyi ɩwɩ adánsɩɛ nɩ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.013', 'Nlɩɩ́ Bulu, ogyi tógyítɔ́ nkpa Ɔhapʋ́ mʋa Kristo Yesuá olegyi ɩwɩ adánsɩɛ wankláán Pontio Pilato ansɩ́tɔ́, ndɛ fʋ́ hɩɛ, ', '', '&lt;span class="verse" id="1TI_6_13"&gt;&lt;strong class="verseno"&gt;13&lt;/strong&gt;&amp;nbsp;Nlɩɩ́ Bulu, ogyi tógyítɔ́ nkpa Ɔhapʋ́ mʋa Kristo Yesuá olegyi ɩwɩ adánsɩɛ wankláán Pontio Pilato ansɩ́tɔ́, ndɛ fʋ́ hɩɛ, &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.014', 'mbɛ́ɛ, fʋkɩ́ta mbla ánfɩ fɛ́ɛ́tɔ́ wankláán. Mási mʋ́tɔ́ kpɛ́fʋn ɛkɛá anɩ Wíe Yesu Kristo ɔbɛ́lɛ ɩwɩ ɔwan. ', '', '&lt;span class="verse" id="1TI_6_14"&gt;&lt;strong class="verseno"&gt;14&lt;/strong&gt;&amp;nbsp;mbɛ́ɛ, fʋkɩ́ta mbla ánfɩ fɛ́ɛ́tɔ́ wankláán. Mási mʋ́tɔ́ kpɛ́fʋn ɛkɛá anɩ Wíe Yesu Kristo ɔbɛ́lɛ ɩwɩ ɔwan. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.015', 'Bulu ɔkʋkʋ́nʋ́ ɔkʋlɛ amʋ ilehián ánɩ́ abɛ́kanfʋ́. Mʋ nkʋlɛ pɛ́ gyí Otúmípʋ, awíe fɛ́ɛ́tɔ́ Owíe. Mʋ ɔbɛ́lɛ Kristo súná brɛ́ wankláán ámʋ́ʋ́ mʋ onutó ɔlɛhɩɛ yáɩ́ ámʋtɔ. ', '', '&lt;span class="verse" id="1TI_6_15"&gt;&lt;strong class="verseno"&gt;15&lt;/strong&gt;&amp;nbsp;Bulu ɔkʋkʋ́nʋ́ ɔkʋlɛ amʋ ilehián ánɩ́ abɛ́kanfʋ́. Mʋ nkʋlɛ pɛ́ gyí Otúmípʋ, awíe fɛ́ɛ́tɔ́ Owíe. Mʋ ɔbɛ́lɛ Kristo súná brɛ́ wankláán ámʋ́ʋ́ mʋ onutó ɔlɛhɩɛ yáɩ́ ámʋtɔ. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.016', 'Mʋ nkʋlɛ pɛ́ tamawú, otsie ɔwankɩ́kpá ánɩ́ ɔhaa tamatálɩ́ kpʋ́kpɛ́ mánta mʋ. Ɔhaa mɔ́kʋ́wun mʋ kɩ, ɔhaa móowun mʋ ɛ́ ɛkɛkɛɛkɛ. Mʋ klɛ gyí obú mʋ́a túmi brɛ́ fɛ́ɛ́! !
', '', '&lt;span class="verse" id="1TI_6_16"&gt;&lt;strong class="verseno"&gt;16&lt;/strong&gt;&amp;nbsp;Mʋ nkʋlɛ pɛ́ tamawú, otsie ɔwankɩ́kpá ánɩ́ ɔhaa tamatálɩ́ kpʋ́kpɛ́ mánta mʋ. Ɔhaa mɔ́kʋ́wun mʋ kɩ, ɔhaa móowun mʋ ɛ́ ɛkɛkɛɛkɛ. Mʋ klɛ gyí obú mʋ́a túmi brɛ́ fɛ́ɛ́! !&lt;p&gt; &lt;/p&gt;&lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.017', 'Bla ɔyɩ́ ánfɩtɔ ɩwɩ anyapʋ́ fɛɛ, bʋmátsu ɩwɩ. Bʋmápʋ ansɩ́ dɩ́nká kɔ́ba anfɩ́ iméetsiá yɔ́ ánfɩsʋ. Mboún bʋpʋ́ʋ ansɩ́ dɩnka Bulusʋ. Mʋtɛ́há anɩ tógyítɔ́á abɔ́pʋnyá ansigyí ɔyɩ́ ánfɩtɔ. ', '', '&lt;span class="verse" id="1TI_6_17"&gt;&lt;strong class="verseno"&gt;17&lt;/strong&gt;&amp;nbsp;Bla ɔyɩ́ ánfɩtɔ ɩwɩ anyapʋ́ fɛɛ, bʋmátsu ɩwɩ. Bʋmápʋ ansɩ́ dɩ́nká kɔ́ba anfɩ́ iméetsiá yɔ́ ánfɩsʋ. Mboún bʋpʋ́ʋ ansɩ́ dɩnka Bulusʋ. Mʋtɛ́há anɩ tógyítɔ́á abɔ́pʋnyá ansigyí ɔyɩ́ ánfɩtɔ. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.018', 'Bla amʋ́ fɛɛ, bʋpʋ́ʋ amʋ́ kɔ́ba bwɛ yilé, amʋ́ yilébwɛ imoni. Bʋsánkɩ ɩbɩtɔ abʋkie atɔ́. ', '', '&lt;span class="verse" id="1TI_6_18"&gt;&lt;strong class="verseno"&gt;18&lt;/strong&gt;&amp;nbsp;Bla amʋ́ fɛɛ, bʋpʋ́ʋ amʋ́ kɔ́ba bwɛ yilé, amʋ́ yilébwɛ imoni. Bʋsánkɩ ɩbɩtɔ abʋkie atɔ́. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.019', 'Nɩ́ bʋdɛ ɩ́nɩ bwɛ á, bʋdɛ atonyahɛ́ yaɩ́ há brɛ́ ámʋ́ʋ́ ɩbá ámʋ. Amʋ́ ɩbɩ bɛ́da nkpa onutó ámʋ.
', '', '&lt;span class="verse" id="1TI_6_19"&gt;&lt;strong class="verseno"&gt;19&lt;/strong&gt;&amp;nbsp;Nɩ́ bʋdɛ ɩ́nɩ bwɛ á, bʋdɛ atonyahɛ́ yaɩ́ há brɛ́ ámʋ́ʋ́ ɩbá ámʋ. Amʋ́ ɩbɩ bɛ́da nkpa onutó ámʋ.&lt;p&gt; &lt;/p&gt;&lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.020', 'Timoteo, kɩ́tá ɩtɔ́ ámʋ́ʋ́ Bulu lahá fʋ́ ámʋ wankláán. Asʋ́n ánɩ́ ɩma asɩ, imosuná Bulusúm mádalɩ fʋ́ ɔnɔ́. Máwa ɩwɩ mimlá nwɛ́ɛngyí ánɩ́ ɩtamasúná Bulusúmtɔ́. Akʋ bʋtɔwá afunu bɛɛ, asʋ́nbɩ igyi. ', '', '&lt;span class="verse" id="1TI_6_20"&gt;&lt;strong class="verseno"&gt;20&lt;/strong&gt;&amp;nbsp;Timoteo, kɩ́tá ɩtɔ́ ámʋ́ʋ́ Bulu lahá fʋ́ ámʋ wankláán. Asʋ́n ánɩ́ ɩma asɩ, imosuná Bulusúm mádalɩ fʋ́ ɔnɔ́. Máwa ɩwɩ mimlá nwɛ́ɛngyí ánɩ́ ɩtamasúná Bulusúmtɔ́. Akʋ bʋtɔwá afunu bɛɛ, asʋ́nbɩ igyi. &lt;/span&gt;');
INSERT INTO "nko_1TI_6" ("version", "book", "verse", "unformatted", "idxfti", "basichtml") VALUES ('nko', '1TI', '6.021', 'Tsúfɛ́ akʋ batsʋn mʋ́sʋ́ sí Kristosʋ hógyi.
Bulu ogyíi mlɩ bʋalɛ.
', '', '&lt;span class="verse" id="1TI_6_21"&gt;&lt;strong class="verseno"&gt;21&lt;/strong&gt;&amp;nbsp;Tsúfɛ́ akʋ batsʋn mʋ́sʋ́ sí Kristosʋ hógyi.&lt;p&gt; &lt;/p&gt;Bulu ogyíi mlɩ bʋalɛ.
&lt;/span&gt;');
COMMIT;