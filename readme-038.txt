ダウンロードありがとうございます

●Tso2Mqo ver 0.38

これはなに
tsoとmqoを相互に変換します。
また複数のtsoを結合することもできます。

v0.33について
v0.40のコードを元にv0.32を再現して既知のバグをいくつか修正したものです。
v0.40のコードは原作者のご厚意により提供されました。

ライセンス
Tso2Mqo v0.32に準じます。

v0.37からの変更点：
・gimp-2.8で書き出した.bmpに対応

v0.36からの変更点：

・metaseq v4系ボーンプラグインに対応
　ボーンと頂点ウェイトをmqx上に書き出します。
  参照tsoを省略するとmqx上からボーンと頂点ウェイトを読み込みます。

v0.35からの変更点：

・metaseq v4.0形式に対応（多角形とボーンはまだ）

v0.34からの変更点：

・エラーメッセージを改善
・読み込む.mqoでのテクスチャ指定が相対パスでも扱える
・書き出す.mqoでのテクスチャ指定を相対パスに変更

v0.33からの変更点：

・四角面に対応
　mqoを読み込むとき四角面を三角面2つに分割します。

v0.32からの変更点：

・platform:x86を指定
　x64環境でNvTriStrip.dllを置き換える必要はなくなりました。

・数値精度はmqo format v1.0に準拠
　mqo上の数値精度は 色:.3f 頂点座標:.4f UV座標:.5f になります。

バグ修正（mqoへの変換）：

・日本語が壊れるバグを修正

　v0.32以前は文字の8bitめを無視して書き込むためcgfxファイルに含まれる
　日本語は壊れています。

　古いtsoを変換する場合の注意点：
　上記の通りv0.32以前に作ったtsoはcgfxに含まれる日本語が壊れています。
　これをv0.33で変換してもcgfxの日本語は修復されません。
　正規のcgfxファイルで置き換えて修復してください。

・材質の境界で頂点が重複するバグを修正

　ひとつのオブジェクトに複数の材質が含まれる場合にv0.32以前は材質の
　境界で頂点が分離します。
　そのためmqoに変換するたび[近接する頂点をくっつける]を実行しないと
　面が不連続になり法線がずれます。

　古いtsoを変換する場合の注意点：
　上記の通りv0.32以前に作ったtsoは材質の境界で面が不連続になります。
　これをv0.33でmqoに変換しても不連続面は修復されません。
　初回は[近接する頂点をくっつける]を実行して修復してください。

--
ぱるた
http://www.pixiv.net/member.php?id=4458432
