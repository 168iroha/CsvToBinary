# CsvToBinary
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/168iroha/CsvToBinary)

CSVファイルからバイナリファイルを作成するためのツールである。
XML形式でCSVファイルからバイナリファイルへの変換方法を定義することで簡単にバイナリファイルを生成することができる。

## 動作条件
- .NET 8.0以降が動作する環境（`master`ブランチのビルド）
- .NET Framework 4.5.1以降が動作する環境（`dotnetframework`ブランチのビルド）

### 使用ライブラリ
本ツールは以下のライブラリを追加で利用している。

- .NET 8.0版
    - [CsvHelper 31.0.4](https://www.nuget.org/packages/CsvHelper/31.0.4/) (MS-PL license OR Apache-2.0 license)
- .NET Framework 4.5.1版
    - [CsvHelper 30.0.1](https://www.nuget.org/packages/CsvHelper/30.0.1/) (MS-PL license OR Apache-2.0 license)
    - [Microsoft.Net.Compilers.Toolset 4.14.0](https://www.nuget.org/packages/Microsoft.Net.Compilers.Toolset/4.14.0/) (MIT License)
    - [PolySharp 1.15.0](https://www.nuget.org/packages/PolySharp/1.15.0/) (MIT License)

## 使い方
基本的な手順は以下に則る。

1. XML形式でCSVファイルからバイナリファイルへの変換方法を定義する。
2. 変換元のCSVファイルを定義する。
3. ツールを実行してバイナリファイルを生成する。

XMLファイルの記述の定義については[`format.xsd`](/CsvToBinary/format.xsd)に全て記述されている。
ツール実行時のコマンドの概要は以下の通りである。ただし、これは実装している各機能を呼び出すために[`Program.cs`](/CsvToBinary/Program.cs)で簡単に実装しただけであるため、必要に応じて好きにカスタマイズしてもよい。

```
CsvToBinary [-csv <ファイル名>] -xml <ファイル名> { [-csv <ファイル名>] -xml <ファイル名> } { -g <キー> <値> }
```

| オプション | 引数 | 説明 |
| - | - | - |
| `-csv` | `<ファイル名>` | 入力するCSVファイルへのパスを指定する。 |
| `-xml` | `<ファイル名>` | 変換方法を示すXMLファイルへのパスを指定する。 |
| `-g` | `<キー>` `<値>` | 外部からKey-Valueデータを設定する。キーが重複する場合は後の値が優先される。 |

いくつかの利用例は以下の記事でも扱っているため参照するといいだろう。

- [バイナリデータをいい感じに作成するツールを作成する - いろはの物置き場](https://168iroha.net/blog/article/202404211923/)

### CSVの単純変換
#### XML
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <!-- CSVファイルのフェッチが完了するまで制限なく繰り返す -->
  <repeat fetch="true">
    <item name="A" />
    <item name="B" />
    <item name="C" />
    <!-- 結果がわかりやすいように改行を挿入する -->
    <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  </repeat>
</format>
```

#### CSV
```csv
A,B,C
1,2,3
4,5,6
7,8,9

```

#### 実行コマンド
```
CsvToBinary -csv data.csv -xml format.xml
```

#### 出力
```
123
456
789

```

### 名前付きパイプからの入力
#### XML
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <!-- CSVファイルのフェッチが完了するまで制限なく繰り返す -->
  <repeat fetch="true">
    <item name="A" />
    <item name="B" />
    <item name="C" />
    <!-- 結果がわかりやすいように改行を挿入する -->
    <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  </repeat>
</format>
```

#### 実行コマンド(Server)
```powershell
# 名前付きパイプでCSVを送信
$pipeServer = New-Object System.IO.Pipes.NamedPipeServerStream('data', [System.IO.Pipes.PipeDirection]::Out)
$pipeServer.WaitForConnection()
$writer = New-Object System.IO.StreamWriter($pipeServer)
$writer.WriteLine("10,11,12")
$writer.WriteLine("13,14,15")
$writer.WriteLine("16,17,18")
$writer.Flush()
$pipeServer.WaitForPipeDrain()
$writer.Dispose()
$pipeServer.Dispose()
```

#### 実行コマンド(Client)
```
CsvToBinary -csv "\\.\pipe\data" -xml format.xml
```

#### 出力
```text
101112
131415
161718

```

### 出力対象のグループ化
#### XML
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <!-- CSVファイルのフェッチが完了するまで制限なく繰り返す -->
  <repeat fetch="true">
    <items name="group">
      <item name="A" />
      <item name="B" />
      <item name="C" />
    </items>
    <!-- 結果がわかりやすいように改行を挿入する -->
    <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  </repeat>
</format>
```

#### CSV
```csv
group/A,group/B,group/C
1,2,3
4,5,6
7,8,9

```

#### 実行コマンド
```
CsvToBinary -csv data.csv -xml format.xml
```

#### 出力
```
123
456
789

```

### 複数のXMLへの分割(逐次結合)
#### XML
##### format.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <item name="A" />
  <!-- 結果がわかりやすいように改行を挿入する -->
  <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  
  <!-- コマンドライン引数で指定した2つ目以降のXMLについて繰り返し -->
  <repeat type="combined-xml">
    <!-- combined-xmlに対応するCSVのレコードについて繰り返し -->
    <repeat type="combined-record">
      <item name="A" />
  
      <!-- 現在解析中の電文ファイルに紐づけられた変換器による解析 -->
      <import type="combined" />

      <item name="B" />
      
      <!-- 結果がわかりやすいように改行を挿入する -->
      <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
    </repeat>
  </repeat>

  <item name="B" />
</format>
```

##### subformat.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <item name="A" />
  <item name="B" />
</format>
```

#### CSV
##### data.csv
```csv
A,B
1,2

```

##### subdata.csv
```csv
A,B
3,4
5,6

```

#### 実行コマンド
```
CsvToBinary -csv data.csv -xml format.xml -csv subdata.csv -xml subformat.xml
```

#### 出力
```
1
1342
1562
2
```

### 複数のXMLへの分割(XML内静的結合)
バイナリファイルの作成前に`import[@type='xml']`の解決を行う方法である。

#### XML
##### format.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <repeat fetch="true">
    <item name="A" />
    <!-- format.xmlからの相対パスで対象XMLファイルを指定 -->
    <import type="xml" target="subformat.xml">
      <!-- 外部からデフォルト値の設定 -->
      <map from="//item[@name='C']/default-value" type="external">外部入力</map>
    </import>
    <item name="B" />
    
    <!-- 結果がわかりやすいように改行を挿入する -->
    <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  </repeat>
</format>
```

##### subformat.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <item name="A" />
  <item name="B" />
  <!-- Cに対してはデフォルト値の設定を外部からできるように指定 -->
  <item name="C"><default-value /></item>
</format>
```

#### CSV
```csv
A,B
1,2
3,4
5,6

```

#### 実行コマンド
```
CsvToBinary -csv data.csv -xml format.xml -g 外部入力 0
```

#### 出力
```
11202
33404
55606

```

### 複数のXMLへの分割(XML内動的結合)
バイナリファイルの作成中に`import[@type='dynamic']`の解決を行う方法である。動的解決の特性上、単にXMLファイルを遅延読み込みをするだけではなく、XPathを利用した高度な解決も可能である。

#### XML
##### format.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <!-- 出力先のファイルの指定 -->
  <writer type="binary-file">
    <item name="ファイル名"><default-value>out.txt</default-value></item>
  </writer>

  <repeat fetch="true">
    <item name="A" />
    <!-- format.xmlからの相対パスで対象XMLファイルを指定 -->
    <import type="dynamic" target="subformat.xml">
      <!-- 外部からデフォルト値の設定 -->
      <map from="//item[@name='C']/default-value" type="external">外部入力</map>
    </import>
    <item name="B" />
    
    <!-- 結果がわかりやすいように改行を挿入する -->
    <item name="" encoding="hexadecimal"><default-value>0D0A</default-value></item>
  </repeat>
</format>
```

##### subformat.xml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<format xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="format.xsd">
  <item name="A" />
  <item name="B" />
  <!-- Cに対してはデフォルト値の設定を外部からできるように指定 -->
  <item name="C"><default-value /></item>
</format>
```

#### CSV
```csv
A,B
1,2
3,4
5,6

```

#### 実行コマンド
```
CsvToBinary -csv data.csv -xml format.xml -g 外部入力 0
```

#### 出力
```
11202
33404
55606

```
