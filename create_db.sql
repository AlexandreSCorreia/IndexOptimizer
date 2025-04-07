-- Scripts abaixo execute um por vez para garantir que atinja o resultado desejavel
-- Objetivo do script é criar uma base e gerar 2 problemas com index para que o banco faça a sugestão de criação deles

-- Script abaixo visualiza a indicaçõa do banco para a criação de index
--SELECT 
--    mid.statement AS Tabela,
--    mid.equality_columns AS ColunasIguais,
--    mid.inequality_columns AS ColunasDiferentes,
--    mid.included_columns AS ColunasIncluidas,
--    migs.avg_user_impact AS ImpactoMedio
--FROM sys.dm_db_missing_index_groups mig
--JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
--JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
--WHERE mid.database_id = DB_ID('LojaProblemIndex')
--AND migs.avg_user_impact > 90
--ORDER BY migs.avg_user_impact DESC;

-- Criação do banco de dados
CREATE DATABASE LojaProblemIndex;
GO

USE LojaProblemIndex;
GO

CREATE TABLE Clientes (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Nome NVARCHAR(200),
    Email NVARCHAR(200),
    DataCadastro DATETIME
);

CREATE TABLE Produtos (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Nome NVARCHAR(200),
    Preco DECIMAL(10,2),
    Estoque INT
);

CREATE TABLE Pedidos (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ClienteId INT,
    DataPedido DATETIME,
    ValorTotal DECIMAL(10,2)
);

CREATE TABLE ItensPedido (
    Id INT PRIMARY KEY IDENTITY(1,1),
    PedidoId INT,
    ProdutoId INT,
    Quantidade INT,
    PrecoUnitario DECIMAL(10,2)
);

DECLARE @i INT = 1;
WHILE @i <= 1000
BEGIN
    INSERT INTO Clientes (Nome, Email, DataCadastro)
    VALUES (CONCAT('Cliente ', @i), CONCAT('cliente', @i, '@exemplo.com'), DATEADD(DAY, -@i, GETDATE()));
    SET @i += 1;
END

SET @i = 1;
WHILE @i <= 500
BEGIN
    INSERT INTO Produtos (Nome, Preco, Estoque)
    VALUES (CONCAT('Produto ', @i), ROUND(RAND() * 100, 2), FLOOR(RAND() * 100));
    SET @i += 1;
END

SET @i = 1;
WHILE @i <= 10000
BEGIN
    DECLARE @clienteId INT = FLOOR(RAND() * 1000) + 1;
    DECLARE @dataPedido DATETIME = DATEADD(DAY, -FLOOR(RAND() * 365), GETDATE());
    DECLARE @valor DECIMAL(10,2) = ROUND(RAND() * 500, 2);

    INSERT INTO Pedidos (ClienteId, DataPedido, ValorTotal)
    VALUES (@clienteId, @dataPedido, @valor);

    SET @i += 1;
END

SET @i = 1;
WHILE @i <= 30000
BEGIN
    DECLARE @pedidoId INT = FLOOR(RAND() * 10000) + 1;
    DECLARE @produtoId INT = FLOOR(RAND() * 500) + 1;
    DECLARE @qtd INT = FLOOR(RAND() * 5) + 1;
    DECLARE @preco DECIMAL(10,2) = ROUND(RAND() * 100, 2);

    INSERT INTO ItensPedido (PedidoId, ProdutoId, Quantidade, PrecoUnitario)
    VALUES (@pedidoId, @produtoId, @qtd, @preco);

    SET @i += 1;
END

-- Limpar caches internos do banco de dados
DBCC FREEPROCCACHE;
GO
DBCC DROPCLEANBUFFERS;
GO

-- O código faz as mesmas 5 consultas 100 vezes, totalizando 500 consultas ao final. É um loop intensivo de leitura do banco de dados.

DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    SELECT * FROM Pedidos WHERE ClienteId = 123;
    SELECT * FROM Pedidos WHERE DataPedido BETWEEN '2023-01-01' AND '2023-12-31';
    SELECT ip.*, p.DataPedido
    FROM ItensPedido ip
    JOIN Pedidos p ON p.Id = ip.PedidoId
    WHERE p.ClienteId = 123;
    SELECT * FROM Clientes WHERE Email = 'cliente123@exemplo.com';
    SELECT * FROM Produtos WHERE Nome LIKE '%Produto 10%';
    SET @i += 1;
END


-- Esse código executa 1.500 consultas no total (3 consultas × 500 repetições), sempre com os mesmos filtros (PedidoId = 100, ClienteId = 123).

DECLARE @i INT = 1;
WHILE @i <= 500
BEGIN
    SELECT * FROM ItensPedido WHERE PedidoId = 100;

    SELECT ip.*, p.DataPedido
    FROM ItensPedido ip
    JOIN Pedidos p ON p.Id = ip.PedidoId
    WHERE ip.PedidoId = 100;

    SELECT * FROM Pedidos WHERE ClienteId = 123;

    SET @i += 1;
END

