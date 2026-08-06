package modules

import (
	"fmt"
	"strings"

	"reficio/internal/firebird"
)

type CorrectionModule struct {
	DB       *firebird.DBConnection
	TableName string
	Columns  []string
}

type SearchResult struct {
	Records []map[string]interface{}
	Count   int
}

func NewCorrectionModule(db *firebird.DBConnection, tableName string) *CorrectionModule {
	return &CorrectionModule{
		DB:        db,
		TableName: tableName,
	}
}

func (m *CorrectionModule) LoadColumns() error {
	columns, err := m.DB.GetColumns(m.TableName)
	if err != nil {
		return err
	}
	m.Columns = columns
	return nil
}

func (m *CorrectionModule) Search(field, value string) (*SearchResult, error) {
	query := fmt.Sprintf("SELECT * FROM %s WHERE %s LIKE ?", m.TableName, field)
	rows, err := m.DB.Query(query, "%"+value+"%")
	if err != nil {
		return nil, err
	}

	records := make([]map[string]interface{}, 0)
	for _, row := range rows.Rows {
		record := make(map[string]interface{})
		for i, col := range rows.Columns {
			record[col] = row[i]
		}
		records = append(records, record)
	}

	return &SearchResult{
		Records: records,
		Count:   len(records),
	}, nil
}

func (m *CorrectionModule) SearchExact(field, value string) (*SearchResult, error) {
	query := fmt.Sprintf("SELECT * FROM %s WHERE %s = ?", m.TableName, field)
	rows, err := m.DB.Query(query, value)
	if err != nil {
		return nil, err
	}

	records := make([]map[string]interface{}, 0)
	for _, row := range rows.Rows {
		record := make(map[string]interface{})
		for i, col := range rows.Columns {
			record[col] = row[i]
		}
		records = append(records, record)
	}

	return &SearchResult{
		Records: records,
		Count:   len(records),
	}, nil
}

func (m *CorrectionModule) SearchByID(idField string, idValue interface{}) (*SearchResult, error) {
	query := fmt.Sprintf("SELECT * FROM %s WHERE %s = ?", m.TableName, idField)
	rows, err := m.DB.Query(query, idValue)
	if err != nil {
		return nil, err
	}

	records := make([]map[string]interface{}, 0)
	for _, row := range rows.Rows {
		record := make(map[string]interface{})
		for i, col := range rows.Columns {
			record[col] = row[i]
		}
		records = append(records, record)
	}

	return &SearchResult{
		Records: records,
		Count:   len(records),
	}, nil
}

func (m *CorrectionModule) SearchByMultiple(fields map[string]interface{}) (*SearchResult, error) {
	conditions := make([]string, 0)
	values := make([]interface{}, 0)

	for field, value := range fields {
		conditions = append(conditions, fmt.Sprintf("%s LIKE ?", field))
		values = append(values, "%"+fmt.Sprintf("%v", value)+"%")
	}

	query := fmt.Sprintf("SELECT * FROM %s WHERE %s", m.TableName, strings.Join(conditions, " AND "))
	rows, err := m.DB.Query(query, values...)
	if err != nil {
		return nil, err
	}

	records := make([]map[string]interface{}, 0)
	for _, row := range rows.Rows {
		record := make(map[string]interface{})
		for i, col := range rows.Columns {
			record[col] = row[i]
		}
		records = append(records, record)
	}

	return &SearchResult{
		Records: records,
		Count:   len(records),
	}, nil
}

func (m *CorrectionModule) UpdateRecord(idField string, idValue interface{}, updates map[string]interface{}) error {
	if len(updates) == 0 {
		return fmt.Errorf("no hay campos para actualizar")
	}

	setClauses := make([]string, 0)
	values := make([]interface{}, 0)

	for field, value := range updates {
		setClauses = append(setClauses, fmt.Sprintf("%s = ?", field))
		values = append(values, value)
	}

	values = append(values, idValue)
	query := fmt.Sprintf("UPDATE %s SET %s WHERE %s = ?", m.TableName, strings.Join(setClauses, ", "), idField)

	_, err := m.DB.Execute(query, values...)
	return err
}

func (m *CorrectionModule) GetRecordCount() (int, error) {
	query := fmt.Sprintf("SELECT COUNT(*) FROM %s", m.TableName)
	result, err := m.DB.Query(query)
	if err != nil {
		return 0, err
	}

	if result.Count > 0 && len(result.Rows) > 0 {
		if count, ok := result.Rows[0][0].(int64); ok {
			return int(count), nil
		}
	}
	return 0, nil
}

func (m *CorrectionModule) GetAllRecords(limit int) (*SearchResult, error) {
	query := fmt.Sprintf("SELECT FIRST %d * FROM %s", limit, m.TableName)
	rows, err := m.DB.Query(query)
	if err != nil {
		return nil, err
	}

	records := make([]map[string]interface{}, 0)
	for _, row := range rows.Rows {
		record := make(map[string]interface{})
		for i, col := range rows.Columns {
			record[col] = row[i]
		}
		records = append(records, record)
	}

	return &SearchResult{
		Records: records,
		Count:   len(records),
	}, nil
}
