package firebird

import (
	"database/sql"
	"fmt"
	"time"

	_ "github.com/nakagami/firebirdsql"
)

type DBConnection struct {
	Host     string
	Port     int
	Path     string
	User     string
	Password string
	pool     *sql.DB
}

type QueryResult struct {
	Columns []string
	Rows    [][]interface{}
	Count   int
}

type FieldUpdate struct {
	Table    string
	IDField  string
	IDValue  interface{}
	FieldName string
	NewValue  interface{}
}

func NewDBConnection(cfg Config) *DBConnection {
	host := "localhost"
	port := 3050
	path := cfg.DBPath

	return &DBConnection{
		Host:     host,
		Port:     port,
		Path:     path,
		User:     cfg.User,
		Password: cfg.Password,
	}
}

func (d *DBConnection) GetDSN() string {
	return fmt.Sprintf("%s:%d/%s", d.Host, d.Port, d.Path)
}

func (d *DBConnection) GetConnection() (*sql.DB, error) {
	if d.pool != nil {
		return d.pool, nil
	}

	dsn := d.GetDSN()
	db, err := sql.Open("firebirdsql", fmt.Sprintf("%s:%s@%s", d.User, d.Password, dsn))
	if err != nil {
		return nil, fmt.Errorf("error al conectar: %w", err)
	}

	db.SetConnMaxLifetime(30 * time.Minute)
	db.SetMaxOpenConns(10)
	db.SetMaxIdleConns(5)

	if err := db.Ping(); err != nil {
		return nil, fmt.Errorf("error al verificar conexion: %w", err)
	}

	d.pool = db
	return db, nil
}

func (d *DBConnection) Connect() (*sql.DB, error) {
	return d.GetConnection()
}

func (d *DBConnection) TestConnection() error {
	db, err := d.GetConnection()
	if err != nil {
		return err
	}
	defer db.Close()
	d.pool = nil
	return nil
}

func (d *DBConnection) Query(sqlStr string, args ...interface{}) (*QueryResult, error) {
	db, err := d.GetConnection()
	if err != nil {
		return nil, err
	}

	rows, err := db.Query(sqlStr, args...)
	if err != nil {
		return nil, fmt.Errorf("error en consulta: %w", err)
	}

	columns, err := rows.Columns()
	if err != nil {
		return nil, fmt.Errorf("error al obtener columnas: %w", err)
	}

	result := &QueryResult{
		Columns: columns,
		Rows:    make([][]interface{}, 0),
	}

	for rows.Next() {
		values := make([]interface{}, len(columns))
		valuePtrs := make([]interface{}, len(columns))
		for i := range values {
			valuePtrs[i] = &values[i]
		}

		if err := rows.Scan(valuePtrs...); err != nil {
			return nil, fmt.Errorf("error al escanear fila: %w", err)
		}

		result.Rows = append(result.Rows, values)
		result.Count++
	}

	return result, nil
}

func (d *DBConnection) Execute(sqlStr string, args ...interface{}) (int64, error) {
	db, err := d.GetConnection()
	if err != nil {
		return 0, err
	}

	result, err := db.Exec(sqlStr, args...)
	if err != nil {
		return 0, fmt.Errorf("error al ejecutar: %w", err)
	}

	rowsAffected, _ := result.RowsAffected()
	return rowsAffected, nil
}

func (d *DBConnection) UpdateField(update FieldUpdate) error {
	sqlStr := fmt.Sprintf("UPDATE %s SET %s = ? WHERE %s = ?", update.Table, update.FieldName, update.IDField)
	_, err := d.Execute(sqlStr, update.NewValue, update.IDValue)
	return err
}

func (d *DBConnection) GetTables() ([]string, error) {
	result, err := d.Query("SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 ORDER BY RDB$RELATION_NAME")
	if err != nil {
		return nil, err
	}

	tables := make([]string, 0, result.Count)
	for _, row := range result.Rows {
		if name, ok := row[0].(string); ok {
			tables = append(tables, name)
		}
	}
	return tables, nil
}

func (d *DBConnection) GetColumns(table string) ([]string, error) {
	result, err := d.Query("SELECT RDB$FIELD_NAME FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = ? ORDER BY RDB$FIELD_POSITION", table)
	if err != nil {
		return nil, err
	}

	columns := make([]string, 0, result.Count)
	for _, row := range result.Rows {
		if name, ok := row[0].(string); ok {
			columns = append(columns, name)
		}
	}
	return columns, nil
}
