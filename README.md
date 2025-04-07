🛠️ IndexOptimizer - Otimizador de Índices SQL Server

📌 Descrição:
Essa aplicação foi desenvolvida para identificar índices ausentes em uma base de dados SQL Server com alto impacto de performance (> 90%) e sugerir ou aplicar esses índices automaticamente. Também executa uma rotina de manutenção para reorganizar e reconstruir índices existentes conforme o nível de fragmentação.

🔄 Modos de operação:
Você pode escolher entre:
- 📝 Geração de Script SQL com todos os comandos (para revisão e execução manual)
- ⚙️ Execução automática dos scripts diretamente no banco de dados, com log salvo em arquivo `.txt`

📥 Argumentos obrigatórios:
  -s   ou --server     : Nome do servidor SQL Server (ex: localhost\SQLEXPRESS)
  -d   ou --database   : Nome do banco de dados
  -u   ou --username   : Nome de usuário do SQL Server
  -p   ou --password   : Senha do SQL Server
  -r   ou --result     : Tipo de retorno: 
                         's' ou 'script' para gerar o script SQL
                         'i' ou 'implement' para aplicar no banco e salvar o log
  -h   ou --help       : Exibe essa mensagem de ajuda

📂 Saída:
- indices.sql          → Gerado se usar o modo "script"
- execucao_log.txt     → Gerado se usar o modo "implementação"

⚠️ Importante:
- Todos os parâmetros são obrigatórios.
- A aplicação faz validação de conexão e alerta em caso de erro.
- Verificações são feitas para evitar criação duplicada de índices (`IF NOT EXISTS`).

💻 Exemplo de uso:
IndexOptimizer.exe -s "localhost" -d "MinhaBase" -u "sa" -p "senha123" -r s

Desenvolvido para facilitar a análise e manutenção de performance em ambientes SQL Server.
