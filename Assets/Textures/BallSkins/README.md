# Assets/Textures/BallSkins — ナンバーテールズ柄のボールテクスチャ

球（`NumberBall`）に貼るキャラクターテクスチャの置き場所。`Assets/Data/BallSkins.asset`（`BallSkinTable`）の
各行 `texture` から参照する。

## ファイル名

```
BallTex_NTS-{Num_Badge}.png
```

- `{Num_Badge}` は創作DB（`100BeautiesLab_CreationsDB/data/Works_NumberTales/DataBases/db_*.json`）の
  **`Num_Badge`** フィールドの値（`BallSkinTable.BallSkin.Badge`）。
- 例: `BallTex_NTS-1.png` / `BallTex_NTS-000.png` / `BallTex_NTS-2B.png` / `BallTex_NTS-10D.png` /
  `BallTex_NTS-3x11.png` / `BallTex_NTS-64XP.png` / `BallTex_NTS-9x9.png`
- **`Num_Badge` は `Num`（`BallSkin.dbNum` / `DbNum`）とは別**。別ボールは `Num` が `2-alt` / `10-alt` / `64-sxp`、
  `Num_Badge` が `2B` / `10D` / `64XP`。ファイル名は `Num_Badge` の方（`Build Ball View Scene` は旧名 `...NTS-{Num}.png` も拾う）。
- **番号ではなく Num_Badge で名付ける**。ロトマシーンの球と別ボールは同じ番号でも別キャラ
  （ロトの `2` = 2(ツグ) → `BallTex_NTS-2.png`／別ボールの `2` = バイナ → `BallTex_NTS-2B.png`）。
- 創作DBの画像命名（`cnsp_imgNTS-1` / `emstk_corefolderNTS-1-1` / `art_imgNTS-1-humanoid`）に合わせた
  `...NTS-{Num_Badge}` 形式。

## 仕様

- UV は `LotteryBallKit` の `BallUV_Template`（球面 UV）に合わせる。
- PNG・sRGB。Unity 側のインポート設定は既定のまま（`NumberBall.SetCharacterTexture` が貼る）。

## 編集用 PSD

PSD は **リポジトリに置かない**。Dropbox 側の作業フォルダで管理し、書き出した PNG だけをここへコピーする
（`Assets/` 内に PSD を置くと Unity がテクスチャとして二重にインポートし、`.meta` と `Library` が膨らむ）。

```
E:\Dropbox\Creative Cloud Files\ナンバーテールズ\LotteryBallKit\BallTex_NTS-{Num_Badge}.psd
```

> フォルダ名が `LotteryBallKit` でも、この PSD／PNG は **CC BY-NC 4.0** のまま（`LotteryBallKit` リポジトリの
> CC BY 4.0 とは別区分）。画像を `LotteryBallKit/` 側へ入れないこと。

## ライセンス

**CC BY-NC 4.0**（リポジトリ既定。`LICENSE.md`）。権利者は 百花繚乱研究所 / ラジアン（RadianN_kswg）。
サブモジュール `LotteryBallKit`（CC BY 4.0）とは条件が違うので、**この画像を LotteryBallKit 側へ入れない**。
